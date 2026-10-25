using Xunit.Abstractions;

namespace Cyclo.Core.Tests.Analyzer;

public class DistanceTests(ITestOutputHelper output) : AnalyzerTestBase(output)
{
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
}
