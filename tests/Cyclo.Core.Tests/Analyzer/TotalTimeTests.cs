using Xunit.Abstractions;

namespace Cyclo.Core.Tests.Analyzer;

public class TotalTimeTests(ITestOutputHelper output) : AnalyzerTestBase(output)
{
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
}
