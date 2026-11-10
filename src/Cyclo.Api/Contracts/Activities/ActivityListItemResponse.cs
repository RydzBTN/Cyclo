namespace Cyclo.Api.Contracts.Activities;

public record ActivityListItemResponse(
    Guid Id,
    string Status,
    DateTimeOffset UploadedAt,
    string FileName,
    float? Distance,
    int? TrainingTimeSeconds,
    float? AvgSpeedMps,
    float? ElevationGainMeters,
    int? AvgHeartRate
    );