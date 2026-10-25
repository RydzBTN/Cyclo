using Xunit.Abstractions;

namespace Cyclo.Core.Tests.Analyzer;

// Whole-summary check on the private ride. Metric-specific real-ride tests live in their own classes.
public class RealActivityTests(ITestOutputHelper output) : AnalyzerTestBase(output)
{
    // References for the same ride:
    //   GPX reader online: 39.29 km, total 4:30:46 (raw GPS sum, like ours).
    //   Garmin (FIT session, GPS only, own filtering): 37.93 km, moving time 2:08:44, timer 2:09:36,
    //   moving avg 17.7 km/h, HR avg 128 / max 146.
    // Our result at the time of writing: activity 2:08:59, 17.59 km/h.
    // Ranges are deliberately not exact: noise filtering will still change the numbers.
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
}
