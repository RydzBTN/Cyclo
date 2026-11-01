namespace Cyclo.Api.Models.Entities;

public class ActivitySummaryEntity
{
    public float Distance { get; set; }
    public float? ActivityDistance { get; set; }
    
    public int TotalSeconds { get; set; }
    public int? ActivitySeconds { get; set; }
    
    public float? AvgSpeed { get; set; }
    
    public int? AvgHeartRate { get; set; }
    public int? MaxHeartRate { get; set; }
    
    public float? Elevation { get; set; }
    
    public BestSegmentEntity? Best1Km { get; set; }
    public BestSegmentEntity? Best5Km { get; set; }
    public BestSegmentEntity? Best10Km { get; set; }
}