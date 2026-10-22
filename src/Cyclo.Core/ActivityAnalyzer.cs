namespace Cyclo.Core;

public static class ActivityAnalyzer
{
    // points must be sorted
    public static ActivitySummary Analyze(IReadOnlyList<TrackPoint> points)
    {
        if (points.Count <= 1) return new ActivitySummary(0, TimeSpan.Zero);
        
        ActivitySummary summary = new ActivitySummary(
            GetDistance(points),
            GetTotalTime(points),
            GetAvgHeartRate(points),
            GetMaxHeartRate(points)
            );
        
        return summary;
    }

    private static float GetDistance(IReadOnlyList<TrackPoint> points)
    {
        double distance = 0;
        
        for (int i = 1; i < points.Count; i++)
        {
            distance += GeoMath.HaversineMeters(points[i - 1].Coordinates, points[i].Coordinates);
        }

        return (float)distance;
    }

    private static TimeSpan GetTotalTime(IReadOnlyList<TrackPoint> points)
    {
        return points[^1].Time -  points[0].Time;
    }

    private static int? GetAvgHeartRate(IReadOnlyList<TrackPoint> points)
    {
        return (int?)points.Average(p => p.HeartRate);
    }

    private static int? GetMaxHeartRate(IReadOnlyList<TrackPoint> points)
    {
        return points.Max(p => p.HeartRate);
    }
}