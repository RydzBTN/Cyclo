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

    // ---------- Edge cases ----------

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

    // ---------- Distance ----------

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

    // ---------- Total time ----------

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

    // Total (elapsed) time includes pauses; activity time is the metric without them.
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

    // ---------- Heart rate ----------

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

    // ---------- Activity (moving) time ----------
    // Points are placed along a meridian, so distances are exact: North(m) is m metres north of 50°N.
    // Pause rule under test: dt <= 0, OR dt > 60 s (the watch stopped recording), OR segment speed < 0.5 m/s.

    [Fact]
    public void Analyze_ContinuousRiding_ActivityTimeEqualsTotalTime()
    {
        TrackPoint[] points =
        [
            Point(North(0), 17.0, Sec(0)),
            Point(North(20), 17.0, Sec(4)),
            Point(North(40), 17.0, Sec(8)),
            Point(North(60), 17.0, Sec(12)),
        ];

        ActivitySummary summary = ActivityAnalyzer.Analyze(points);
        Print(points, summary);

        Assert.Equal(TimeSpan.FromSeconds(12), summary.ActivityTime);
    }

    // Auto-pause in the watch: long gap, almost no displacement (real ride: 9 min, 2.6 m).
    [Fact]
    public void Analyze_LongStopWithoutMovement_IsExcludedFromActivityTime()
    {
        TrackPoint[] points =
        [
            Point(North(0), 17.0, Sec(0)),
            Point(North(20), 17.0, Sec(4)),
            Point(North(22.6), 17.0, Sec(4 + 540)),      // 9 min stop, 2.6 m drift
            Point(North(42.6), 17.0, Sec(4 + 540 + 4)),
        ];

        ActivitySummary summary = ActivityAnalyzer.Analyze(points);
        Print(points, summary);

        Assert.Equal(TimeSpan.FromSeconds(548), summary.TotalTime);
        Assert.Equal(TimeSpan.FromSeconds(8), summary.ActivityTime);
    }

    // Deliberate decision: smart recording never leaves gaps longer than ~20 s while riding,
    // so a gap > 60 s means the watch was paused – even if the position moved a lot.
    // Real ride: 18 min gap with 894 m of walking/slow riding, which Garmin's timer did not count either.
    // Trade-off: GPS loss in a tunnel longer than 60 s is also treated as a pause.
    [Fact]
    public void Analyze_RecordingGapLongerThan60s_IsPauseEvenWithMovement()
    {
        TrackPoint[] points =
        [
            Point(North(0), 17.0, Sec(0)),
            Point(North(20), 17.0, Sec(4)),
            Point(North(1020), 17.0, Sec(4 + 180)),      // 1 km in 3 min (~5.6 m/s), but no recording in between
            Point(North(1040), 17.0, Sec(4 + 180 + 4)),
        ];

        ActivitySummary summary = ActivityAnalyzer.Analyze(points);
        Print(points, summary);

        Assert.Equal(TimeSpan.FromSeconds(8), summary.ActivityTime);
    }

    // Boundary of the recording-gap rule: 59 s with normal riding speed is still activity...
    [Fact]
    public void Analyze_RecordingGapJustBelow60s_IsActivity()
    {
        TrackPoint[] points =
        [
            Point(North(0), 17.0, Sec(0)),
            Point(North(295), 17.0, Sec(59)),            // 5 m/s
        ];

        ActivitySummary summary = ActivityAnalyzer.Analyze(points);
        Print(points, summary);

        Assert.Equal(TimeSpan.FromSeconds(59), summary.ActivityTime);
    }

    // ...and 61 s is a pause.
    [Fact]
    public void Analyze_RecordingGapJustAbove60s_IsPause()
    {
        TrackPoint[] points =
        [
            Point(North(0), 17.0, Sec(0)),
            Point(North(305), 17.0, Sec(61)),            // 5 m/s
        ];

        ActivitySummary summary = ActivityAnalyzer.Analyze(points);
        Print(points, summary);

        Assert.Equal(TimeSpan.Zero, summary.ActivityTime);
    }

    // 4 m in 10 s = 0.4 m/s → below the 0.5 m/s threshold.
    [Fact]
    public void Analyze_SegmentJustBelowSpeedThreshold_IsPause()
    {
        TrackPoint[] points =
        [
            Point(North(0), 17.0, Sec(0)),
            Point(North(20), 17.0, Sec(4)),
            Point(North(24), 17.0, Sec(14)),
        ];

        ActivitySummary summary = ActivityAnalyzer.Analyze(points);
        Print(points, summary);

        Assert.Equal(TimeSpan.FromSeconds(4), summary.ActivityTime);
    }

    // 6 m in 10 s = 0.6 m/s → above the threshold (e.g. walking the bike uphill is still activity).
    [Fact]
    public void Analyze_SegmentJustAboveSpeedThreshold_IsActivity()
    {
        TrackPoint[] points =
        [
            Point(North(0), 17.0, Sec(0)),
            Point(North(20), 17.0, Sec(4)),
            Point(North(26), 17.0, Sec(14)),
        ];

        ActivitySummary summary = ActivityAnalyzer.Analyze(points);
        Print(points, summary);

        Assert.Equal(TimeSpan.FromSeconds(14), summary.ActivityTime);
    }

    // Two points with the same timestamp happen in real files. dt = 0 must not produce NaN / Infinity.
    [Fact]
    public void Analyze_DuplicateTimestamp_DoesNotBreakActivityTimeOrSpeed()
    {
        TrackPoint[] points =
        [
            Point(North(0), 17.0, Sec(0)),
            Point(North(20), 17.0, Sec(4)),
            Point(North(25), 17.0, Sec(4)),              // same time as previous point
            Point(North(45), 17.0, Sec(8)),
        ];

        ActivitySummary summary = ActivityAnalyzer.Analyze(points);
        Print(points, summary);

        Assert.Equal(TimeSpan.FromSeconds(8), summary.ActivityTime);
        Assert.NotNull(summary.AvgSpeed);
        Assert.True(float.IsFinite(summary.AvgSpeed.Value), $"AvgSpeed is {summary.AvgSpeed}");
    }

    // KNOWN LIMITATION, documented on purpose: a watch WITHOUT auto-pause records every second while
    // standing, and GPS jitter (~2 m per sample) looks like 2 m/s riding. The per-segment rule cannot see it.
    // When noise filtering / windowed speed is added, flip this test to expect ActivityTime == 0.
    [Fact]
    public void Analyze_GpsJitterWhileStanding_IsCountedAsActivity_KnownLimitation()
    {
        TrackPoint[] points =
        [
            Point(North(0), 17.0, Sec(0)),
            Point(North(2), 17.0, Sec(1)),
            Point(North(0), 17.0, Sec(2)),
            Point(North(2), 17.0, Sec(3)),
            Point(North(0), 17.0, Sec(4)),
        ];

        ActivitySummary summary = ActivityAnalyzer.Analyze(points);
        Print(points, summary);

        Assert.Equal(TimeSpan.FromSeconds(4), summary.ActivityTime);
    }

    // ---------- Average speed ----------

    [Fact]
    public void Analyze_ConstantSpeed_AvgSpeedEqualsThatSpeed()
    {
        TrackPoint[] points =
        [
            Point(North(0), 17.0, Sec(0)),
            Point(North(50), 17.0, Sec(10)),
            Point(North(100), 17.0, Sec(20)),
            Point(North(150), 17.0, Sec(30)),
        ];

        ActivitySummary summary = ActivityAnalyzer.Analyze(points);
        Print(points, summary);

        Assert.Equal(5.0, summary.AvgSpeed!.Value, tolerance: 0.01);
    }

    // Average speed = distance / time, NOT the average of segment speeds.
    // Segments: 100 m in 10 s (10 m/s), 100 m in 50 s (2 m/s), 100 m in 25 s (4 m/s).
    //   correct:                    300 m / 85 s      = 3.53 m/s
    //   mean of segment speeds:     (10 + 2 + 4) / 3  = 5.33 m/s
    [Fact]
    public void Analyze_DifferentSpeeds_AvgSpeedIsDistanceDividedByTime()
    {
        TrackPoint[] points =
        [
            Point(North(0), 17.0, Sec(0)),
            Point(North(100), 17.0, Sec(10)),
            Point(North(200), 17.0, Sec(60)),
            Point(North(300), 17.0, Sec(85)),
        ];

        ActivitySummary summary = ActivityAnalyzer.Analyze(points);
        Print(points, summary);

        Assert.Equal(300.0 / 85.0, summary.AvgSpeed!.Value, tolerance: 0.01);
    }

    // A 10-minute stop must not drag the average speed down (the 8.71 vs 17.56 km/h problem).
    [Fact]
    public void Analyze_StopInTheMiddle_IsExcludedFromAvgSpeed()
    {
        TrackPoint[] points =
        [
            Point(North(0), 17.0, Sec(0)),
            Point(North(100), 17.0, Sec(20)),
            Point(North(101), 17.0, Sec(20 + 600)),      // 10 min stop, 1 m drift
            Point(North(201), 17.0, Sec(20 + 600 + 20)),
        ];

        ActivitySummary summary = ActivityAnalyzer.Analyze(points);
        Print(points, summary);

        Assert.Equal(5.0, summary.AvgSpeed!.Value, tolerance: 0.01); // 200 m in 40 s of riding
    }

    // ---------- Real ride (private file) ----------

    // Private ride, not committed to the repo – excluded in CI with: dotnet test --filter "Category!=LocalData"
    // References for the same ride:
    //   GPX reader online: 39.29 km, total 4:30:46 (raw GPS sum, like ours).
    //   Garmin (FIT session, GPS only, own filtering): 37.93 km, timer 2:09:36, 17.56 km/h, HR avg 128 / max 146.
    // Our result at the time of writing: activity 2:08:59, 17.59 km/h.
    // Ranges are deliberately not exact: noise filtering and elevation smoothing will still change the numbers.
    [Fact]
    [Trait("Category", "LocalData")]
    public void Analyze_RealActivity_MatchesReferenceValues()
    {
        IReadOnlyList<TrackPoint> points = LoadPrivateRide();

        ActivitySummary summary = ActivityAnalyzer.Analyze(points);
        Print(points, summary);

        Assert.Equal(1579, points.Count);
        Assert.Equal(new TimeSpan(4, 30, 46), summary.TotalTime);
        Assert.Equal(39_290, summary.Distance, tolerance: 1_000);

        Assert.InRange(summary.ActivityTime!.Value, new TimeSpan(2, 5, 0), new TimeSpan(2, 15, 0));
        Assert.InRange(summary.AvgSpeed!.Value * 3.6f, 16.5f, 18.5f); // km/h

        // Garmin's HR average is time-weighted and excludes pauses; ours is a plain average of points.
        Assert.InRange(summary.AvgHeartRate!.Value, 118, 138);
        Assert.InRange(summary.MaxHeartRate!.Value, 140, 150);
    }

    // ---------- Helpers ----------

    private static IReadOnlyList<TrackPoint> LoadPrivateRide()
    {
        string path = Path.Combine(AppContext.BaseDirectory, "TestData", "Private", "activity_23911368613.gpx");
        using Stream stream = File.OpenRead(path);
        return GpxParser.ParseTrackPoints(stream);
    }

    private static TrackPoint Point(double lat, double lon, DateTimeOffset time, int? hr = null) =>
        new(new Coordinates(Lat: lat, Lon: lon), time, HeartRate: hr);

    private static DateTimeOffset At(int hour, int minute, int second) =>
        new(2026, 8, 9, hour, minute, second, TimeSpan.Zero);

    // Time = 10:00:00 + seconds. Keeps test data readable as "how many seconds into the ride".
    private static DateTimeOffset Sec(int seconds) => At(10, 0, 0).AddSeconds(seconds);

    // Latitude of a point the given number of metres north of 50°N (along a meridian 1° = OneDegreeArcMeters).
    private static double North(double meters) => 50.0 + meters / OneDegreeArcMeters;

    private void Print(IReadOnlyList<TrackPoint> points, ActivitySummary summary)
    {
        _output.WriteLine($"{"#",3} | {"time",8} | {"lat",10} | {"lon",10} | {"segment [m]",12} | {"total [m]",12} | {"dt [s]",7} | {"speed [m/s]",11}");
        _output.WriteLine(new string('-', 96));

        double total = 0;
        for (int i = 0; i < points.Count; i++)
        {
            double segment = i == 0 ? 0 : GeoMath.HaversineMeters(points[i - 1].Coordinates, points[i].Coordinates);
            double dt = i == 0 ? 0 : (points[i].Time - points[i - 1].Time).TotalSeconds;
            string speed = dt > 0 ? (segment / dt).ToString("F2") : "-";
            total += segment;

            _output.WriteLine(
                $"{i,3} | {points[i].Time:HH:mm:ss} | {points[i].Coordinates.Lat,10:F5} | {points[i].Coordinates.Lon,10:F5} | {segment,12:F2} | {total,12:F2} | {dt,7:F0} | {speed,11}");
        }

        _output.WriteLine("");
        _output.WriteLine($"Summary: Distance = {summary.Distance:F2} m, TotalTime = {summary.TotalTime}, ActivityTime = {summary.ActivityTime}, " +
                          $"AvgSpeed = {summary.AvgSpeed:F2} m/s, AvgHeartRate = {summary.AvgHeartRate}, MaxHeartRate = {summary.MaxHeartRate}");
    }
}
