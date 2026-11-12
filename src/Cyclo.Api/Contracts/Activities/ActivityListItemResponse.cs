namespace Cyclo.Api.Contracts.Activities;

public record ActivityListItemResponse(
    Guid Id,
    string Status,
    DateTimeOffset UploadedAt,
    string FileName,
    float? ActivityDistanceMeters,
    int? ActivitySeconds,
    float? AvgSpeedMps,
    float? ElevationGainMeters,
    int? AvgHeartRate
    );