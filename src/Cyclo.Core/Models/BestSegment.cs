namespace Cyclo.Core.Models;

public record BestSegment(
    DateTimeOffset StartTime,
    DateTimeOffset EndTime,
    float AvgSpeed
    );