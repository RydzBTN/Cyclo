namespace Cyclo.Core;

public record ActivitySummary(
    float Distance,
    TimeSpan TotalTime,
    float? ActivityDistance = null,
    TimeSpan? ActivityTime = null,
    float? AvgSpeed = null,
    int? AvgHeartRate = null,
    int? MaxHeartRate = null,
    float? Elevation = null,
    BestSegment? Best1Km = null,
    BestSegment? Best5Km = null,
    BestSegment? Best10Km = null
    );