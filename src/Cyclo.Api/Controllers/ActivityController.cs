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
    public IActionResult Get(Guid id)
    {
        return NotFound();
    }
    
    [HttpPost]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(10 * 1024 * 1024)] // 10MB
    public async Task<IActionResult> UploadActivity(IFormFile file, CancellationToken cancellationToken)
    {
        if (file.Length == 0) return BadRequest("file is empty");
        if (!string.Equals(Path.GetExtension(file.FileName), ".gpx", StringComparison.OrdinalIgnoreCase))
            return BadRequest("file is not gpx");
        if (file.FileName.Length > 255) return BadRequest("file name is too long");
        
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
            return Conflict("this file already exists"); // or 303
        }

        return AcceptedAtAction(
            actionName: nameof(Get),
            routeValues: new { id = activity.Id },
            value: new ActivityCreatedResponse(activity.Id, activity.Status.ToString())
        );
    }
}