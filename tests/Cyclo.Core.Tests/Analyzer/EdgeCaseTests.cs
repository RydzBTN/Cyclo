using Xunit.Abstractions;

namespace Cyclo.Core.Tests.Analyzer;

public class EdgeCaseTests(ITestOutputHelper output) : AnalyzerTestBase(output)
{
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
}
