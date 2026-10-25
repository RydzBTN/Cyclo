namespace Cyclo.Core;

public record BestSegment(
    DateTimeOffset StartTime,
    DateTimeOffset EndTime,
    float AvgSpeed
    );