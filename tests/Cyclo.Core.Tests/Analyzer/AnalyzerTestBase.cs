using Xunit.Abstractions;

namespace Cyclo.Core.Tests.Analyzer;

// Shared helpers for all ActivityAnalyzer test classes (one class per metric).
public abstract class AnalyzerTestBase
{
    protected const double EarthRadiusMeters = 6_371_000;
    protected const double OneDegreeArcMeters = 2 * Math.PI * EarthRadiusMeters / 360; // ~111 194.93 m

    // xUnit captures output per test; Console.WriteLine is not shown in test results.
    protected readonly ITestOutputHelper Output;

    protected AnalyzerTestBase(ITestOutputHelper output)
    {
        Output = output;
    }

    // Private ride, not committed to the repo – tests using it are marked [Trait("Category", "LocalData")]
    // and excluded in CI with: dotnet test --filter "Category!=LocalData"
    protected static IReadOnlyList<TrackPoint> LoadPrivateRide()
    {
        string path = Path.Combine(AppContext.BaseDirectory, "TestData", "Private", "activity_23911368613.gpx");
        using Stream stream = File.OpenRead(path);
        return GpxParser.ParseTrackPoints(stream);
    }

    protected static TrackPoint Point(double lat, double lon, DateTimeOffset time, int? hr = null, float? ele = null) =>
        new(new Coordinates(Lat: lat, Lon: lon), time, Elevation: ele, HeartRate: hr);

    // Ride along a meridian built from parts: (number of segments, metres per segment, seconds per segment).
    protected static TrackPoint[] Ride(params (int Segments, double Meters, int Seconds)[] parts)
    {
        var points = new List<TrackPoint> { Point(North(0), 17.0, Sec(0)) };
        double meters = 0;
        int seconds = 0;
        foreach (var (segments, segmentMeters, segmentSeconds) in parts)
        {
            for (int k = 0; k < segments; k++)
            {
                meters += segmentMeters;
                seconds += segmentSeconds;
                points.Add(Point(North(meters), 17.0, Sec(seconds)));
            }
        }
        return points.ToArray();
    }

    // Moving points 20 m apart every 4 s (5 m/s – never a pause) with the given elevations.
    protected static TrackPoint[] Profile(params float?[] elevations) =>
        elevations.Select((ele, i) => Point(North(20 * i), 17.0, Sec(4 * i), ele: ele)).ToArray();

    protected static DateTimeOffset At(int hour, int minute, int second) =>
        new(2026, 8, 9, hour, minute, second, TimeSpan.Zero);

    // Time = 10:00:00 + seconds. Keeps test data readable as "how many seconds into the ride".
    protected static DateTimeOffset Sec(int seconds) => At(10, 0, 0).AddSeconds(seconds);

    // Latitude of a point the given number of metres north of 50°N (along a meridian 1° = OneDegreeArcMeters).
    protected static double North(double meters) => 50.0 + meters / OneDegreeArcMeters;

    protected void Print(IReadOnlyList<TrackPoint> points, ActivitySummary summary)
    {
        Output.WriteLine($"{"#",3} | {"time",8} | {"lat",10} | {"lon",10} | {"segment [m]",12} | {"total [m]",12} | {"dt [s]",7} | {"speed [m/s]",11} | {"ele [m]",7}");
        Output.WriteLine(new string('-', 106));

        double total = 0;
        for (int i = 0; i < points.Count; i++)
        {
            double segment = i == 0 ? 0 : GeoMath.HaversineMeters(points[i - 1].Coordinates, points[i].Coordinates);
            double dt = i == 0 ? 0 : (points[i].Time - points[i - 1].Time).TotalSeconds;
            string speed = dt > 0 ? (segment / dt).ToString("F2") : "-";
            total += segment;

            Output.WriteLine(
                $"{i,3} | {points[i].Time:HH:mm:ss} | {points[i].Coordinates.Lat,10:F5} | {points[i].Coordinates.Lon,10:F5} | {segment,12:F2} | {total,12:F2} | {dt,7:F0} | {speed,11} | {points[i].Elevation?.ToString("F1") ?? "-",7}");
        }

        Output.WriteLine("");
        Output.WriteLine($"Summary: Distance = {summary.Distance:F2} m, TotalTime = {summary.TotalTime}, ActivityTime = {summary.ActivityTime}, " +
                         $"AvgSpeed = {summary.AvgSpeed:F2} m/s, AvgHeartRate = {summary.AvgHeartRate}, MaxHeartRate = {summary.MaxHeartRate}, " +
                         $"Elevation = {summary.Elevation?.ToString() ?? "null"} m, " +
                         $"Best1km = {summary.Best1Km?.AvgSpeed.ToString("F2") ?? "null"} m/s");
    }
}
