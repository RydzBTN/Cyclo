namespace Cyclo.Api.Data.Entities;

public class BestSegmentEntity
{
    public DateTimeOffset StartTime { get; set; }
    public DateTimeOffset EndTime { get; set; }
    public float AvgSpeed { get; set; }
}