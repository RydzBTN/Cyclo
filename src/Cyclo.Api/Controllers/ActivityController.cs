using System.ComponentModel.DataAnnotations;
using System.IO.Compression;
using System.Security.Cryptography;
using Cyclo.Api.Auth;
using Cyclo.Api.Contracts.Activities;
using Cyclo.Api.Data;
using Cyclo.Api.Data.Entities.Activity;
using Cyclo.Api.Pagination;
using Cyclo.Api.Processing;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Cyclo.Api.Controllers;

[Authorize]
[ApiController]
[Route("activities")]
public class ActivityController(CycloDbContext dbContext, ActivityQueue activityQueue) : ControllerBase
{
    [HttpGet("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<ActivityResponse>> Get(Guid id, CancellationToken cancellationToken)
    {
        Guid userId = User.GetUserId();
        
        ActivityResponse? response = await dbContext.Activities
            .Where(x => x.Id == id && x.UserId == userId)
            .Select(x => new ActivityResponse(
                x.Id,
                x.Status.ToString(),
                x.UploadedAt,
                x.ProcessingStartedAt,
                x.ProcessingEndedAt,
                x.FileName,
                x.FailureReason,
                x.Summary == null ? null : new ActivitySummaryResponse(
                    x.Summary.DistanceMeters,
                    x.Summary.ActivityDistanceMeters,
                    x.Summary.TotalSeconds,
                    x.Summary.ActivitySeconds,
                    x.Summary.AvgSpeedMps,
                    x.Summary.AvgHeartRate,
                    x.Summary.MaxHeartRate,
                    x.Summary.ElevationGainMeters, 
                    x.Summary.Best1Km == null ? null : new BestSegmentResponse(
                        x.Summary.Best1Km.StartTime,
                        x.Summary.Best1Km.EndTime,
                        x.Summary.Best1Km.AvgSpeedMps),
                    x.Summary.Best5Km == null ? null : new BestSegmentResponse(
                        x.Summary.Best5Km.StartTime,
                        x.Summary.Best5Km.EndTime,
                        x.Summary.Best5Km.AvgSpeedMps),
                    x.Summary.Best10Km == null ? null : new BestSegmentResponse(
                        x.Summary.Best10Km.StartTime,
                        x.Summary.Best10Km.EndTime,
                        x.Summary.Best10Km.AvgSpeedMps)
                    )
                )
            )
            .FirstOrDefaultAsync(cancellationToken);

        if (response is null) return NotFound();
        
        return Ok(response);
    }
    
    [HttpGet]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<ActivityPageResponse>> GetPage(
        [FromQuery] string? cursor,
        [FromQuery, Range(1, 50)] int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        Guid userId = User.GetUserId();
        
        IQueryable<ActivityEntity> query = dbContext.Activities.Where(x => x.UserId == userId);

        if (!string.IsNullOrEmpty(cursor))
        {
            if (!ActivityCursor.TryDecode(cursor, out DateTimeOffset cursorDate, out Guid id))
            {
                ModelState.AddModelError(nameof(cursor), "cannot decode cursor");
                return ValidationProblem(ModelState);
            }

            query = query.Where(x => x.UploadedAt < cursorDate || (x.UploadedAt == cursorDate && x.Id.CompareTo(id) < 0));
        }

        List<ActivityListItemResponse> items = await query.OrderByDescending(x => x.UploadedAt)
            .ThenByDescending(x => x.Id)
            .Take(pageSize + 1)
            .Select(x => new ActivityListItemResponse(
                x.Id,
                x.Status.ToString(),
                x.UploadedAt,
                x.FileName,
                x.Summary == null ? null : x.Summary.ActivityDistanceMeters,
                x.Summary == null ? null : x.Summary.ActivitySeconds,
                x.Summary == null ? null : x.Summary.AvgSpeedMps,
                x.Summary == null ? null : x.Summary.ElevationGainMeters,
                x.Summary == null ? null : x.Summary.AvgHeartRate
            ))
            .ToListAsync(cancellationToken);

        bool hasMore = items.Count > pageSize;
        if (hasMore) items.RemoveAt(pageSize);

        string? nextCursor = hasMore ? ActivityCursor.Encode(items[^1].UploadedAt, items[^1].Id) : null;
        
        return Ok(new ActivityPageResponse(items, nextCursor));
    }
    
    
    [HttpPost]
    [Consumes("multipart/form-data")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable)]
    [ProducesResponseType(StatusCodes.Status415UnsupportedMediaType)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ActivityCreatedResponse>(StatusCodes.Status202Accepted)]
    [RequestSizeLimit(10 * 1024 * 1024)] // 10MB
    public async Task<IActionResult> UploadActivity(IFormFile file, CancellationToken cancellationToken)
    {
        if (file.Length == 0) ModelState.AddModelError(nameof(file), "File is empty");

        if (!string.Equals(Path.GetExtension(file.FileName), ".gpx", StringComparison.OrdinalIgnoreCase))
            ModelState.AddModelError(nameof(file), "File doesn't have gpx extension");
        
        if (file.FileName.Length > 255)
            ModelState.AddModelError(nameof(file), "File name is longer than 255 characters");
        
        if (!ModelState.IsValid) return ValidationProblem(ModelState);

        if (activityQueue.IsFull)
        {
            Response.Headers.RetryAfter = activityQueue.RetryAfterSeconds.ToString();
            return Problem(
                statusCode: StatusCodes.Status503ServiceUnavailable,
                detail: "Server is overloaded");
        }
        
        string hash;
        byte[] compressed;
        
        await using (var stream = file.OpenReadStream())
        {
            byte[] hashBytes = await SHA256.HashDataAsync(stream, cancellationToken);
            hash = Convert.ToHexStringLower(hashBytes);
        }
        
        await using (var stream = file.OpenReadStream())
        {
            using var destination = new MemoryStream();
            await using (Stream compressor = new GZipStream(destination, CompressionLevel.Optimal))
            {
                await stream.CopyToAsync(compressor, cancellationToken);
            }

            compressed = destination.ToArray();
        }
        
        var activity = new ActivityEntity
        {
            UserId = User.GetUserId(),
            Status = ProcessStatus.Pending,
            UploadedAt = DateTimeOffset.UtcNow,
            FileName = file.FileName,
            OriginalSizeBytes = file.Length,
            FileHash = hash
        };
        dbContext.Add(activity);
        
        var activityFile = new ActivityFileEntity
        {
            ActivityId = activity.Id,
            Data = compressed
        };
        dbContext.Add(activityFile);
        
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException {Number: 2601 or 2627})
        {
            return Problem(
                statusCode: StatusCodes.Status409Conflict,
                detail: "this file already exists");
        }
        
        _ = activityQueue.TryEnqueue(activity.Id); // if request survived activityQueue.IsFull, auto sweeper will calculate summary
        
        return AcceptedAtAction(
            actionName: nameof(Get),
            routeValues: new { id = activity.Id },
            value: new ActivityCreatedResponse(activity.Id, activity.Status.ToString())
        );
    }
    
}