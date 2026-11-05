namespace Cyclo.Api.Contracts.Activities;

public record ActivitySummaryResponse(
    float DistanceMeters,
    float? ActivityDistanceMeters,
    int TotalSeconds,
    int? ActivitySeconds,
    float? AvgSpeedMps,
    int? AvgHeartRate,
    int? MaxHeartRate,
    float? ElevationGainMeters,
    BestSegmentResponse? Best1Km,
    BestSegmentResponse? Best5Km,
    BestSegmentResponse? Best10Km
    );