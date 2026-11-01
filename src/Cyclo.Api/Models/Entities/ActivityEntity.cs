namespace Cyclo.Api.Models.Entities;

public class ActivityEntity
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public ProcessStatus Status { get; set; }
    
    public DateTimeOffset UploadedAt { get; set; }
    public DateTimeOffset? ProcessingStartedAt { get; set; }
    public DateTimeOffset? ProcessingEndedAt { get; set; }
    
    public required string FileName { get; set; }
    public required string FileHash { get; set; }
    
    public ActivitySummaryEntity? Summary { get; set; }
}

