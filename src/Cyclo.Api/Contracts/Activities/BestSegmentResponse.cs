namespace Cyclo.Api.Contracts.Activities;

public record BestSegmentResponse(
    DateTimeOffset StartTime,
    DateTimeOffset EndTime,
    float AvgSpeedMps
    );