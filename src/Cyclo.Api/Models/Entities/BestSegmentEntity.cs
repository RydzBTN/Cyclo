namespace Cyclo.Api.Models.Entities;

public class BestSegmentEntity
{
    public DateTimeOffset StartTime { get; set; }
    public DateTimeOffset EndTime { get; set; }
    public float AvgSpeed { get; set; }
}