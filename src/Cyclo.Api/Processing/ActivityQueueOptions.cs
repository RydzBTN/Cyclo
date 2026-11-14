using System.ComponentModel.DataAnnotations;

namespace Cyclo.Api.Processing;

public class ActivityQueueOptions
{
    public const string SectionName = "ActivityQueue";
    
    [Range(1, 1_000_000)]
    public int Capacity { get; set; }
    
    [Range(1, 3600)]
    public int RetryAfterSeconds { get; set; }
    
    [Range(1, 3600)] 
    public int StuckProcessingTimeoutSeconds { get; set; }

    [Range(1, 3600)] // < timeout
    public int SweepIntervalSeconds { get; set; }
}