namespace Cyclo.Core.Models;

public readonly record struct TrackPoint(
    Coordinates Coordinates,
    DateTimeOffset Time,
    float? Elevation = null,
    float? Temperature = null,
    int? HeartRate = null);