using System.IO.Compression;
using System.Security.Cryptography;
using Cyclo.Api.Contracts.Activities;
using Cyclo.Api.Data;
using Cyclo.Api.Data.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Cyclo.Api.Controllers;

[ApiController]
[Route("activities")]
public class ActivityController(CycloDbContext dbContext) : ControllerBase
{
    private const string TempUserId = "user";
    
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken)
    {
        ActivityResponse? response = await dbContext.Activities
            .Where(x => x.Id == id && x.UserId == TempUserId)
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
        
        if (response is null) return Problem(
            statusCode: StatusCodes.Status404NotFound);
        
        return Ok(response);
    }
    
    [HttpPost]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(10 * 1024 * 1024)] // 10MB
    public async Task<IActionResult> UploadActivity(IFormFile file, CancellationToken cancellationToken)
    {
        if (file.Length == 0) ModelState.AddModelError(nameof(file), "File is empty");

        if (!string.Equals(Path.GetExtension(file.FileName), ".gpx", StringComparison.OrdinalIgnoreCase))
            ModelState.AddModelError(nameof(file), "File doesn't have gpx extension");
        
        if (file.FileName.Length > 255)
            ModelState.AddModelError(nameof(file), "File name is longer than 255 characters");
        
        if (!ModelState.IsValid) return ValidationProblem(ModelState);
        
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
            UserId = TempUserId, // TODO: replace with JWT sub
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

        return AcceptedAtAction(
            actionName: nameof(Get),
            routeValues: new { id = activity.Id },
            value: new ActivityCreatedResponse(activity.Id, activity.Status.ToString())
        );
    }
}