using Xunit.Abstractions;

namespace Cyclo.Core.Tests;

public class ActivityAnalyzerTests
{
    private const double EarthRadiusMeters = 6_371_000;
    private const double OneDegreeArcMeters = 2 * Math.PI * EarthRadiusMeters / 360; // ~111 194.93 m
    
    private readonly ITestOutputHelper _output;

    public ActivityAnalyzerTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void Analyze_NoPoints_ReturnsZeros()
    {
        var points = Array.Empty<TrackPoint>();

        ActivitySummary summary = ActivityAnalyzer.Analyze(points);
        Print(points, summary);

        Assert.Equal(0, summary.Distance);
        Assert.Equal(TimeSpan.Zero, summary.TotalTime);
    }

    [Fact]
    public void Analyze_SinglePoint_ReturnsZeros()
    {
        TrackPoint[] points = [Point(50.0, 17.0, At(8, 0, 0))];

        ActivitySummary summary = ActivityAnalyzer.Analyze(points);
        Print(points, summary);

        Assert.Equal(0, summary.Distance);
        Assert.Equal(TimeSpan.Zero, summary.TotalTime);
    }

    [Fact]
    public void Analyze_TwoPointsOneDegreeApartOnEquator_ReturnsOneDegreeArc()
    {
        TrackPoint[] points =
        [
            Point(0, 0, At(8, 0, 0)),
            Point(0, 1, At(9, 0, 0)),
        ];

        ActivitySummary summary = ActivityAnalyzer.Analyze(points);
        Print(points, summary);

        // Distance is a float, so allow ~1 m of rounding at this magnitude.
        Assert.Equal(OneDegreeArcMeters, summary.Distance, tolerance: 1);
    }

    // Fails if the loop skips the first or the last segment (classic off-by-one).
    [Fact]
    public void Analyze_FourPointsAlongEquator_SumsAllSegments()
    {
        TrackPoint[] points =
        [
            Point(0, 0, At(8, 0, 0)),
            Point(0, 1, At(8, 10, 0)),
            Point(0, 2, At(8, 20, 0)),
            Point(0, 3, At(8, 30, 0)),
        ];

        ActivitySummary summary = ActivityAnalyzer.Analyze(points);
        Print(points, summary);

        Assert.Equal(3 * OneDegreeArcMeters, summary.Distance, tolerance: 1);
    }

    // Fails if distance is measured "as the crow flies" from first to last point instead of along the track.
    [Fact]
    public void Analyze_OutAndBack_CountsBothDirections()
    {
        TrackPoint[] points =
        [
            Point(0, 0, At(8, 0, 0)),
            Point(0, 1, At(8, 30, 0)),
            Point(0, 0, At(9, 0, 0)),
        ];

        ActivitySummary summary = ActivityAnalyzer.Analyze(points);
        Print(points, summary);

        Assert.Equal(2 * OneDegreeArcMeters, summary.Distance, tolerance: 1);
    }

    [Fact]
    public void Analyze_PointsAtSamePosition_ReturnsZeroDistance()
    {
        TrackPoint[] points =
        [
            Point(50.0, 17.0, At(8, 0, 0)),
            Point(50.0, 17.0, At(8, 0, 10)),
            Point(50.0, 17.0, At(8, 0, 20)),
        ];

        ActivitySummary summary = ActivityAnalyzer.Analyze(points);
        Print(points, summary);

        Assert.Equal(0, summary.Distance, tolerance: 1e-6);
    }

    // Fails if total time is calculated from neighbouring points instead of first and last.
    [Fact]
    public void Analyze_ThreePoints_TotalTimeIsLastMinusFirst()
    {
        TrackPoint[] points =
        [
            Point(50.0, 17.0, At(8, 0, 0)),
            Point(50.001, 17.0, At(8, 0, 10)),
            Point(50.002, 17.0, At(8, 1, 0)),
        ];

        ActivitySummary summary = ActivityAnalyzer.Analyze(points);
        Print(points, summary);

        Assert.Equal(TimeSpan.FromSeconds(60), summary.TotalTime);
    }
    
    // Documents the current behaviour: total (elapsed) time includes pauses.
    // Moving time will be a separate metric.
    [Fact]
    public void Analyze_LongGapBetweenPoints_IsIncludedInTotalTime()
    {
        TrackPoint[] points =
        [
            Point(50.0, 17.0, At(10, 0, 0)),
            Point(50.001, 17.0, At(10, 4, 37)),
            Point(50.001, 17.0, At(11, 16, 40)), // 72 min gap
            Point(50.002, 17.0, At(11, 17, 0)),
        ];

        ActivitySummary summary = ActivityAnalyzer.Analyze(points);
        Print(points, summary);

        Assert.Equal(new TimeSpan(1, 17, 0), summary.TotalTime);
    }
    
    // Private ride, not committed to the repo – excluded in CI with: dotnet test --filter "Category!=LocalData"
    // Reference values: GPX reader online and Garmin's own summary (FIT session) for the same ride.
    [Fact]
    [Trait("Category", "LocalData")]
    public void Analyze_RealActivity_MatchesReferenceValues()
    {
        string path = Path.Combine(AppContext.BaseDirectory, "TestData", "Private", "activity_23911368613.gpx");
        using Stream stream = File.OpenRead(path);

        IReadOnlyList<TrackPoint> points = GpxParser.ParseTrackPoints(stream);
        ActivitySummary summary = ActivityAnalyzer.Analyze(points);

        Print(points, summary);

        Assert.Equal(1579, points.Count);
        Assert.Equal(new TimeSpan(4, 30, 46), summary.TotalTime);
        // Reader online: 39.29 km. Garmin: 37.93 km, but measured with a wheel sensor, not GPS.
        Assert.Equal(39_290, summary.Distance, tolerance: 1_000);
        // Garmin: avg 128 / max 146 bpm. Garmin's average is time-weighted and excludes pauses,
        // ours is a plain average of points – so only a sanity range for now.
        Assert.InRange(summary.AvgHeartRate!.Value, 118, 138);
        Assert.InRange(summary.MaxHeartRate!.Value, 140, 150);
    }

    [Fact]
    public void Analyze_AllPointsWithHeartRate_ReturnsAverageAndMax()
    {
        TrackPoint[] points =
        [
            Point(50.0, 17.0, At(10, 0, 0), hr: 100),
            Point(50.001, 17.0, At(10, 4, 37), hr: 100),
            Point(50.001, 17.0, At(11, 16, 40), hr: 140),
            Point(50.002, 17.0, At(11, 17, 0), hr: 100),
        ];

        ActivitySummary summary = ActivityAnalyzer.Analyze(points);
        Print(points, summary);

        Assert.Equal(110, summary.AvgHeartRate);
        Assert.Equal(140, summary.MaxHeartRate);
    }
    
    // No chest strap: "no data" must stay null, not become 0 bpm.
    [Fact]
    public void Analyze_NoHeartRateData_ReturnsNullHeartRate()
    {
        TrackPoint[] points =
        [
            Point(50.0, 17.0, At(10, 0, 0)),
            Point(50.001, 17.0, At(10, 0, 10)),
            Point(50.002, 17.0, At(10, 0, 20)),
        ];

        ActivitySummary summary = ActivityAnalyzer.Analyze(points);
        Print(points, summary);

        Assert.Null(summary.AvgHeartRate);
        Assert.Null(summary.MaxHeartRate);
    }

    // Strap lost contact for a while: missing values are skipped, not counted as 0.
    // If nulls were treated as 0, the average would be 60 instead of 120.
    [Fact]
    public void Analyze_SomePointsWithoutHeartRate_IgnoresMissingValues()
    {
        TrackPoint[] points =
        [
            Point(50.0, 17.0, At(10, 0, 0), hr: 100),
            Point(50.001, 17.0, At(10, 0, 10)),
            Point(50.002, 17.0, At(10, 0, 20), hr: 140),
            Point(50.003, 17.0, At(10, 0, 30)),
        ];

        ActivitySummary summary = ActivityAnalyzer.Analyze(points);
        Print(points, summary);

        Assert.Equal(120, summary.AvgHeartRate);
        Assert.Equal(140, summary.MaxHeartRate);
    }

    // Documents a deliberate decision: the average is truncated, not rounded (1 bpm is irrelevant for training).
    [Fact]
    public void Analyze_FractionalAverageHeartRate_IsTruncated()
    {
        TrackPoint[] points =
        [
            Point(50.0, 17.0, At(10, 0, 0), hr: 120),
            Point(50.001, 17.0, At(10, 0, 10), hr: 121),
        ];

        ActivitySummary summary = ActivityAnalyzer.Analyze(points);
        Print(points, summary);

        Assert.Equal(120, summary.AvgHeartRate); // 120.5 → 120
    }


    private static TrackPoint Point(double lat, double lon, DateTimeOffset time, int? hr = null) =>
        new(new Coordinates(Lat: lat, Lon: lon), time, HeartRate: hr);

    private static DateTimeOffset At(int hour, int minute, int second) =>
        new(2026, 8, 9, hour, minute, second, TimeSpan.Zero);

    private void Print(IReadOnlyList<TrackPoint> points, ActivitySummary summary)
    {
        _output.WriteLine($"{"#",3} | {"time",8} | {"lat",10} | {"lon",10} | {"segment [m]",12} | {"total [m]",12}");
        _output.WriteLine(new string('-', 70));

        double total = 0;
        for (int i = 0; i < points.Count; i++)
        {
            double segment = i == 0 ? 0 : GeoMath.HaversineMeters(points[i - 1].Coordinates, points[i].Coordinates);
            total += segment;

            _output.WriteLine(
                $"{i,3} | {points[i].Time:HH:mm:ss} | {points[i].Coordinates.Lat,10:F5} | {points[i].Coordinates.Lon,10:F5} | {segment,12:F2} | {total,12:F2}");
        }

        _output.WriteLine("");
        _output.WriteLine($"Summary: Distance = {summary.Distance:F2} m, TotalTime = {summary.TotalTime},  AvgHeartRate = {summary.AvgHeartRate}, MaxHeartRate = {summary.MaxHeartRate}");
    }
}
