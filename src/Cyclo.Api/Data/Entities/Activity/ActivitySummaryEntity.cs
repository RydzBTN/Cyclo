namespace Cyclo.Api.Data.Entities.Activity;

public class ActivitySummaryEntity
{
    public float DistanceMeters { get; set; }
    public float? ActivityDistanceMeters { get; set; }
    
    public int TotalSeconds { get; set; }
    public int? ActivitySeconds { get; set; }
    
    public float? AvgSpeedMps { get; set; }
    
    public int? AvgHeartRate { get; set; }
    public int? MaxHeartRate { get; set; }
    
    public float? ElevationGainMeters { get; set; }
    
    public BestSegmentEntity? Best1Km { get; set; }
    public BestSegmentEntity? Best5Km { get; set; }
    public BestSegmentEntity? Best10Km { get; set; }
}