using Xunit.Abstractions;

namespace Cyclo.Core.Tests.Analyzer;

public class HeartRateTests(ITestOutputHelper output) : AnalyzerTestBase(output)
{
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
}
