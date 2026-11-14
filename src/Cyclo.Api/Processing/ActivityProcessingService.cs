using System.IO.Compression;
using Cyclo.Api.Data;
using Cyclo.Api.Data.Entities.Activity;
using Cyclo.Core.Analysis;
using Cyclo.Core.Models;
using Cyclo.Core.Parsing;
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
    
    private async Task ProcessAsync(Guid activityId, CancellationToken cancellationToken)
    {
        await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
        CycloDbContext db = scope.ServiceProvider.GetRequiredService<CycloDbContext>();

        DateTimeOffset nowStarted = DateTimeOffset.UtcNow;
        
        int rows = await db.Activities
            .Where(x => x.Id == activityId && x.Status == ProcessStatus.Pending)
            .ExecuteUpdateAsync(x => x
                    .SetProperty(y => y.Status, ProcessStatus.Processing)
                    .SetProperty(y => y.ProcessingStartedAt, nowStarted),
                cancellationToken);
        
        if (rows == 0)
        {
            logger.LogDebug("Activity {ActivityId} already claimed or missing, skipping", activityId);
            return;
        }
        logger.LogInformation("Activity {ActivityId} claimed", activityId);
        
        byte[]? compressedFile = await db.ActivityFiles
            .Where(x => x.ActivityId == activityId)
            .Select(x => x.Data)
            .FirstOrDefaultAsync(cancellationToken);
        
        if(compressedFile == null)
        {
            await SetFailedAsync(db, activityId, $"Internal processing error");
            logger.LogError("Activity {activityId} in database is empty", activityId);
            return;
        }
        
        using MemoryStream source = new MemoryStream(compressedFile);
        await using Stream decompressor = new GZipStream(source, CompressionMode.Decompress);
        
        IReadOnlyList<TrackPoint> points = new List<TrackPoint>();
        try
        {
            points = GpxParser.ParseTrackPoints(decompressor);
        }
        catch (InvalidGpxException ex)
        {
            await SetFailedAsync(db, activityId, ex.Message);
            return;
        }
        
        if (points.Count < 2)
        {
            await SetFailedAsync(db, activityId, "Activity contains less than 2 track points");
            return;
        }
        
        ActivitySummary summary = ActivityAnalyzer.Analyze(points);
        
        ActivitySummaryEntity summaryEntity = new ActivitySummaryEntity
        {
            DistanceMeters = summary.Distance,
            ActivityDistanceMeters = summary.ActivityDistance,
            TotalSeconds = (int)summary.TotalTime.TotalSeconds,
            ActivitySeconds = (int?)summary.ActivityTime?.TotalSeconds,
            AvgSpeedMps = summary.AvgSpeed,
            AvgHeartRate = summary.AvgHeartRate,
            MaxHeartRate = summary.MaxHeartRate,
            ElevationGainMeters = summary.Elevation,
            Best1Km = summary.Best1Km == null ? null : new BestSegmentEntity
            {
                StartTime = summary.Best1Km.StartTime,
                EndTime = summary.Best1Km.EndTime,
                AvgSpeedMps = summary.Best1Km.AvgSpeed
            },
            Best5Km = summary.Best5Km == null ? null : new BestSegmentEntity
            {
                StartTime = summary.Best5Km.StartTime,
                EndTime = summary.Best5Km.EndTime,
                AvgSpeedMps = summary.Best5Km.AvgSpeed
            },
            Best10Km = summary.Best10Km == null ? null : new BestSegmentEntity
            {
                StartTime = summary.Best10Km.StartTime,
                EndTime = summary.Best10Km.EndTime,
                AvgSpeedMps = summary.Best10Km.AvgSpeed
            }
        };
        
        ActivityEntity? activityEntity = await db.Activities
            .Where(x => x.Id == activityId)
            .FirstOrDefaultAsync(cancellationToken);
        if (activityEntity == null) throw new Exception($"Cant find activity in database with id: {activityId}");

        var nowEnded = DateTimeOffset.UtcNow;
        activityEntity.Status = ProcessStatus.Completed;
        activityEntity.ProcessingEndedAt = nowEnded;
        activityEntity.Summary = summaryEntity;
        
        await db.SaveChangesAsync(CancellationToken.None);

        logger.LogInformation("Activity {ActivityId} completed in {ElapsedMs} ms", activityId, (nowEnded - nowStarted).TotalMilliseconds);
    }

    private async Task SetFailedAsync(CycloDbContext db, Guid activityId, string failureReason)
    {
        var now = DateTimeOffset.UtcNow;
        await db.Activities.Where(x => x.Id == activityId)
            .ExecuteUpdateAsync(x => x
                    .SetProperty(y => y.Status, ProcessStatus.Failed)
                    .SetProperty(y => y.FailureReason, failureReason)
                    .SetProperty(y => y.ProcessingEndedAt, now),
                CancellationToken.None);
    }
}

















