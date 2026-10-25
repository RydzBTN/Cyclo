using Xunit.Abstractions;

namespace Cyclo.Core.Tests.Analyzer;

// Profile(...) builds moving points (20 m every 4 s = 5 m/s, never a pause) with the given elevations.
// Test data uses noise of ±1 m and real changes of 5+ m (threshold in the analyzer: 1.1 m).
public class ElevationTests(ITestOutputHelper output) : AnalyzerTestBase(output)
{
    [Fact]
    public void Analyze_FlatProfileWithNoise_ReturnsZeroElevationGain()
    {
        TrackPoint[] points = Profile(100, 101, 100, 101, 100, 101, 100);

        ActivitySummary summary = ActivityAnalyzer.Analyze(points);
        Print(points, summary);

        Assert.Equal(0, summary.Elevation);
    }

    [Fact]
    public void Analyze_SteadyClimb_ReturnsTotalClimb()
    {
        TrackPoint[] points = Profile(100, 105, 110, 115, 120, 125, 130, 135, 140, 145, 150);

        ActivitySummary summary = ActivityAnalyzer.Analyze(points);
        Print(points, summary);

        Assert.Equal(50, summary.Elevation);
    }

    // Fails if the starting altitude itself is counted as a climb (anchor not initialised from the first point).
    [Fact]
    public void Analyze_StartAtHighAltitude_DoesNotCountStartingAltitude()
    {
        TrackPoint[] points = Profile(800, 805, 810);

        ActivitySummary summary = ActivityAnalyzer.Analyze(points);
        Print(points, summary);

        Assert.Equal(10, summary.Elevation);
    }

    [Fact]
    public void Analyze_DescentOnly_ReturnsZeroElevationGain()
    {
        TrackPoint[] points = Profile(150, 140, 130, 120, 110, 100);

        ActivitySummary summary = ActivityAnalyzer.Analyze(points);
        Print(points, summary);

        Assert.Equal(0, summary.Elevation);
    }

    // Fails if the anchor does not follow a clear descent: the second climb would be counted only above 110 m.
    [Fact]
    public void Analyze_UpDownUp_CountsBothClimbs()
    {
        TrackPoint[] points = Profile(100, 110, 90, 110);

        ActivitySummary summary = ActivityAnalyzer.Analyze(points);
        Print(points, summary);

        Assert.Equal(30, summary.Elevation); // +10, then +20
    }

    // Noise on top of a real climb: only the climb counts.
    [Fact]
    public void Analyze_ClimbWithNoise_CountsOnlyRealClimb()
    {
        TrackPoint[] points = Profile(100, 101, 100, 101, 110, 109, 110, 109, 120, 121, 120);

        ActivitySummary summary = ActivityAnalyzer.Analyze(points);
        Print(points, summary);

        Assert.InRange(summary.Elevation!.Value, 19, 21);
    }

    // Documents a decision: a rise below the threshold at the very end is not counted.
    [Fact]
    public void Analyze_SmallRiseAtTheEnd_IsNotCounted()
    {
        TrackPoint[] points = Profile(100, 110, 111);

        ActivitySummary summary = ActivityAnalyzer.Analyze(points);
        Print(points, summary);

        Assert.Equal(10, summary.Elevation);
    }

    [Fact]
    public void Analyze_PointsWithoutElevationInTheMiddle_AreSkipped()
    {
        TrackPoint[] points = Profile(100, null, 110, null, null, 120);

        ActivitySummary summary = ActivityAnalyzer.Analyze(points);
        Print(points, summary);

        Assert.Equal(20, summary.Elevation);
    }

    // Decision from the design: no elevation data at all → null, not 0 m (same as heart rate).
    [Fact]
    public void Analyze_NoElevationData_ReturnsNullElevationGain()
    {
        TrackPoint[] points = Profile(null, null, null, null);

        ActivitySummary summary = ActivityAnalyzer.Analyze(points);
        Print(points, summary);

        Assert.Null(summary.Elevation);
    }

    // Barometer drifts with the weather during a long stop (here +5 m in 10 min). The drift must not count.
    // Without re-anchoring after the pause the result would be 25 instead of 20.
    [Fact]
    public void Analyze_ElevationDriftDuringPause_IsNotCounted()
    {
        TrackPoint[] points =
        [
            Point(North(0), 17.0, Sec(0), ele: 100),
            Point(North(20), 17.0, Sec(4), ele: 110),             // +10, riding
            Point(North(21), 17.0, Sec(4 + 600), ele: 115),       // 10 min pause, +5 m drift
            Point(North(41), 17.0, Sec(4 + 600 + 4), ele: 125),   // +10, riding
        ];

        ActivitySummary summary = ActivityAnalyzer.Analyze(points);
        Print(points, summary);

        Assert.Equal(20, summary.Elevation);
    }

    // Private ride. References: GPX reader online 237 m (naive sum of every rise), Garmin 99 m.
    // Prints the naive sum computed here for comparison – the gap between the two is the noise we filter out.
    [Fact]
    [Trait("Category", "LocalData")]
    public void Analyze_RealActivity_ElevationGainCloseToGarmin()
    {
        IReadOnlyList<TrackPoint> points = LoadPrivateRide();

        ActivitySummary summary = ActivityAnalyzer.Analyze(points);

        Output.WriteLine($"Naive sum of rises: {NaiveElevationGain(points):F1} m   (GPX reader: 237 m)");
        Output.WriteLine($"Our elevation gain: {summary.Elevation} m   (Garmin: 99 m)");

        // Sanity range only – tighten it once you have seen the real output.
        Assert.InRange(summary.Elevation!.Value, 70, 140);
    }

    // Reference for the real-ride test: sum of every rise between consecutive points that have elevation.
    // This is what simple GPX readers show – no threshold, no pause handling.
    private static double NaiveElevationGain(IReadOnlyList<TrackPoint> points)
    {
        double gain = 0;
        float? previous = null;
        foreach (TrackPoint p in points)
        {
            if (p.Elevation is not float current) continue;
            if (previous is float prev && current > prev) gain += current - prev;
            previous = current;
        }
        return gain;
    }
}
