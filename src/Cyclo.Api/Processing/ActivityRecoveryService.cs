using Cyclo.Api.Data;
using Cyclo.Api.Data.Entities.Activity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Cyclo.Api.Processing;

public class ActivityRecoveryService
(
    ActivityQueue queue,
    IOptions<ActivityQueueOptions> queueOptions,
    IServiceScopeFactory scopeFactory,
    ILogger<ActivityRecoveryService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            // First sweep after App Run. All activities with Pending/Processing status are orphaned
            await SweepAsync(0, stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            logger.LogInformation("Sweep cancelled");
            return;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Sweep failed ");
        }
        
        
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(queueOptions.Value.SweepIntervalSeconds));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await SweepAsync(queueOptions.Value.StuckProcessingTimeoutSeconds, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                logger.LogInformation("Sweep cancelled");
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Sweep failed ");
            }
        }
    }

    private async Task SweepAsync(int unlockThreshold, CancellationToken ct)
    {
        await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
        CycloDbContext db = scope.ServiceProvider.GetRequiredService<CycloDbContext>();
        
        
        // locked Processing
        var now = DateTimeOffset.UtcNow;
        var cutoff =  now - TimeSpan.FromSeconds(unlockThreshold);
        int rows = await db.Activities
            .Where(x =>
                x.Status == ProcessStatus.Processing &&
                x.ProcessingStartedAt < cutoff)
            .ExecuteUpdateAsync(x => x
                    .SetProperty(y => y.Status, ProcessStatus.Pending)
                    .SetProperty(y => y.ProcessingStartedAt, (DateTimeOffset?)null),
                ct);
        if(rows > 0) logger.LogWarning("unlocked {Rows} Processing rows", rows);
        
        
        // orphaned Pending
        Guid[] ids = await db.Activities
            .Where(x => x.Status == ProcessStatus.Pending)
            .OrderBy(x => x.UploadedAt)
            .Take(queueOptions.Value.Capacity)
            .Select(x => x.Id)
            .ToArrayAsync(ct);

        int i = 0;
        foreach (Guid id in ids)
        {
            if(!queue.TryEnqueue(id)) return;
            i++;
        }
        logger.LogInformation("{AddedGuids} Id's has been readded to activity processing queue", i);
    }
}