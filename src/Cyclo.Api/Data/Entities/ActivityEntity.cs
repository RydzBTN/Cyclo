namespace Cyclo.Api.Data.Entities;

public class ActivityEntity
{
    public Guid Id { get; set; }
    public required string UserId { get; set; }
    public ProcessStatus Status { get; set; }
    
    public required DateTimeOffset UploadedAt { get; set; }
    public DateTimeOffset? ProcessingStartedAt { get; set; }
    public DateTimeOffset? ProcessingEndedAt { get; set; }
    
    public required string FileName { get; set; }
    public required long OriginalSizeBytes { get; set; }
    public required string FileHash { get; set; }
    
    public ActivitySummaryEntity? Summary { get; set; }
}

