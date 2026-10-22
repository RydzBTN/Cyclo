namespace Cyclo.Core;

public record ActivitySummary(
    float Distance,
    TimeSpan TotalTime,
    float? ActivityDistance = null,
    TimeSpan? ActivityTime = null,
    float? AvgSpeed = null,
    int? AvgHeartRate = null,
    int? MaxHeartRate = null
    );