using Cyclo.Api.Data;
using Cyclo.Api.Data.Entities.Activity;
using Microsoft.EntityFrameworkCore;

namespace Cyclo.Api.Processing;

public class ActivityProcessingService(
    ActivityQueue queue,
    IServiceScopeFactory scopeFactory,
    ILogger<ActivityProcessingService> logger
    ) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (Guid activityId in queue.ReadAllAsync(stoppingToken))
        {
            try
            {
                await ProcessAsync(activityId, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                logger.LogInformation("Processing cancelled for {ActivityId}", activityId);
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Processing failed for {ActivityId}", activityId);
            }
        }
    }
    
    private async Task ProcessAsync(Guid id, CancellationToken cancellationToken)
    {
        await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
        CycloDbContext db = scope.ServiceProvider.GetRequiredService<CycloDbContext>();

        DateTimeOffset now = DateTimeOffset.UtcNow;
        
        int rows = await db.Activities
            .Where(x => x.Id == id && x.Status == ProcessStatus.Pending)
            .ExecuteUpdateAsync(x => x
                    .SetProperty(y => y.Status, ProcessStatus.Processing)
                    .SetProperty(y => y.ProcessingStartedAt, now),
                cancellationToken);
        
        if (rows == 0)
        {
            logger.LogDebug("Activity {ActivityId} already claimed or missing, skipping", id);
            return;  
        }
        
        logger.LogInformation("Activity {ActivityId} claimed", id);
        
        
        
        
    }
}