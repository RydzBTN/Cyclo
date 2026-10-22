namespace Cyclo.Core;

public record ActivitySummary(
    float Distance,
    TimeSpan TotalTime,
    int? AvgHeartRate = null,
    int? MaxHeartRate = null
    );