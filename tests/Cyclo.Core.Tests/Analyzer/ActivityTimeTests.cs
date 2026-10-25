using Xunit.Abstractions;

namespace Cyclo.Core.Tests.Analyzer;

// Points are placed along a meridian, so distances are exact: North(m) is m metres north of 50°N.
// Pause rule under test: dt <= 0, OR dt > 60 s (the watch stopped recording), OR segment speed < 0.5 m/s.
public class ActivityTimeTests(ITestOutputHelper output) : AnalyzerTestBase(output)
{
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
}
