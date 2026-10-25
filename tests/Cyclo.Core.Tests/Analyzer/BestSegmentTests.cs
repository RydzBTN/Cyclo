using Xunit.Abstractions;

namespace Cyclo.Core.Tests.Analyzer;

// Ride(...) builds a track along a meridian from parts: (segments, metres per segment, seconds per segment).
// Segments are 110 m long on purpose: a window of 10 segments is 1100 m (>= 1000), 9 segments is 990 m (< 1000),
// so the result never depends on floating-point rounding exactly at 1000 m.
public class BestSegmentTests(ITestOutputHelper output) : AnalyzerTestBase(output)
{
    // At constant 5 m/s every window is 5 m/s. Fails if speed is computed from the target distance (1000 m)
    // instead of the real window distance (1100 m): that would report 4.55 m/s.
    [Fact]
    public void Analyze_ConstantSpeed_Best1kmSpeedEqualsThatSpeed()
    {
        TrackPoint[] points = Ride((20, 110, 22)); // 2.2 km at 5 m/s

        ActivitySummary summary = ActivityAnalyzer.Analyze(points);
        Print(points, summary);

        Assert.NotNull(summary.Best1Km);
        Assert.Equal(5.0, summary.Best1Km.AvgSpeed, tolerance: 0.01);
    }

    // Slow – fast – slow. Fails if some start points are skipped or the window time is measured from the wrong point.
    [Fact]
    public void Analyze_FasterSectionInTheMiddle_Best1kmFindsIt()
    {
        TrackPoint[] points = Ride(
            (10, 110, 22),   // 1.1 km at 5 m/s,  t = 0 → 220 s
            (10, 110, 11),   // 1.1 km at 10 m/s, t = 220 → 330 s
            (10, 110, 22));  // 1.1 km at 5 m/s

        ActivitySummary summary = ActivityAnalyzer.Analyze(points);
        Print(points, summary);

        Assert.NotNull(summary.Best1Km);
        Assert.Equal(Sec(220), summary.Best1Km.StartTime);
        Assert.Equal(Sec(330), summary.Best1Km.EndTime);
        Assert.Equal(10.0, summary.Best1Km.AvgSpeed, tolerance: 0.01);
    }

    [Fact]
    public void Analyze_RideShorterThan1km_ReturnsNoBestSegments()
    {
        TrackPoint[] points = Ride((9, 100, 20)); // 900 m

        ActivitySummary summary = ActivityAnalyzer.Analyze(points);
        Print(points, summary);

        Assert.Null(summary.Best1Km);
        Assert.Null(summary.Best5Km);
        Assert.Null(summary.Best10Km);
    }

    [Fact]
    public void Analyze_Ride6km_HasBest1kmAnd5kmButNo10km()
    {
        TrackPoint[] points = Ride((55, 110, 22)); // 6.05 km at 5 m/s

        ActivitySummary summary = ActivityAnalyzer.Analyze(points);
        Print(points, summary);

        Assert.NotNull(summary.Best1Km);
        Assert.NotNull(summary.Best5Km);
        Assert.Equal(5.0, summary.Best5Km.AvgSpeed, tolerance: 0.01);
        Assert.Null(summary.Best10Km);
    }

    // Watch paused (gap > 60 s) and the rider moved 800 m in the meantime, e.g. by car.
    // A window across that gap would look like 11+ m/s. It must not count: windows break at recording gaps.
    [Fact]
    public void Analyze_RecordingGapInsideWindow_DoesNotCreateFakeBestSegment()
    {
        TrackPoint[] points = Ride(
            (5, 110, 11),    // 550 m at 10 m/s,         t = 0 → 55 s
            (1, 800, 61),    // recording gap: 800 m jump, t = 55 → 116 s
            (5, 110, 11),    // 550 m at 10 m/s,         t = 116 → 171 s
            (2, 110, 60),    // slow: 1.8 m/s,           t = 171 → 291 s
            (10, 110, 20));  // 1.1 km at 5.5 m/s,       t = 291 → 491 s  ← the only legit candidate

        ActivitySummary summary = ActivityAnalyzer.Analyze(points);
        Print(points, summary);

        Assert.NotNull(summary.Best1Km);
        Assert.Equal(Sec(291), summary.Best1Km.StartTime);
        Assert.Equal(Sec(491), summary.Best1Km.EndTime);
        Assert.Equal(5.5, summary.Best1Km.AvgSpeed, tolerance: 0.01);
    }

    // Private ride (~39 km). Garmin max speed was 37.4 km/h (10.4 m/s), moving average 17.7 km/h (4.9 m/s),
    // so the best 1 km must be somewhere in between. Sanity range only – tighten after seeing the output.
    [Fact]
    [Trait("Category", "LocalData")]
    public void Analyze_RealActivity_FindsAllBestSegments()
    {
        IReadOnlyList<TrackPoint> points = LoadPrivateRide();

        ActivitySummary summary = ActivityAnalyzer.Analyze(points);

        foreach (var (name, segment) in new[] { ("1 km", Best1km: summary.Best1Km), ("5 km", Best5km: summary.Best5Km), ("10 km", Best10km: summary.Best10Km) })
        {
            Output.WriteLine(segment is null
                ? $"Best {name}: none"
                : $"Best {name}: {segment.StartTime:HH:mm:ss} → {segment.EndTime:HH:mm:ss}, " +
                  $"{segment.AvgSpeed:F2} m/s ({segment.AvgSpeed * 3.6:F1} km/h)");
        }

        Assert.NotNull(summary.Best1Km);
        Assert.NotNull(summary.Best5Km);
        Assert.NotNull(summary.Best10Km);
        Assert.InRange(summary.Best1Km.AvgSpeed, 4.9f, 10.4f);
        // A longer best segment can never be faster than the best shorter one inside it.
        Assert.True(summary.Best1Km.AvgSpeed >= summary.Best5Km.AvgSpeed);
        Assert.True(summary.Best5Km.AvgSpeed >= summary.Best10Km.AvgSpeed);
    }
}
