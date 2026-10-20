namespace Cyclo.Core;

public readonly record struct TrackPoint(
    Coordinates Coordinates,
    DateTimeOffset Time,
    float? Elevation = null,
    float? Temperature = null,
    int? HeartRate = null);