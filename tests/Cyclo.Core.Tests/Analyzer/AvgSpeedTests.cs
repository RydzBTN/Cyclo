using Xunit.Abstractions;

namespace Cyclo.Core.Tests.Analyzer;

public class AvgSpeedTests(ITestOutputHelper output) : AnalyzerTestBase(output)
{
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
}
