namespace Cyclo.Api.Data.Entities.Activity;

public class BestSegmentEntity
{
    public DateTimeOffset StartTime { get; set; }
    public DateTimeOffset EndTime { get; set; }
    public float AvgSpeedMps { get; set; }
}