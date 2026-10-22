namespace Cyclo.Core;

public static class ActivityAnalyzer
{
    private static readonly int MaxRecordingGap = 60;
    
    // points must be sorted
    public static ActivitySummary Analyze(IReadOnlyList<TrackPoint> points)
    {
        if (points.Count <= 1) return new ActivitySummary(0, TimeSpan.Zero);
        
        float activityDistance = GetActivityDistance(points);
        TimeSpan activityTime = GetActivityTime(points);
        float avgSpeed = (float)(activityDistance / activityTime.TotalSeconds);
        
        ActivitySummary summary = new ActivitySummary(
            GetDistance(points),
            GetTotalTime(points),
            activityDistance,
            activityTime,
            float.IsNaN(avgSpeed) ? null : avgSpeed,
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

    private static float GetActivityDistance(IReadOnlyList<TrackPoint> points)
    {
        double distance = 0;
        
        for (int i = 1; i < points.Count; i++)
        {
            if (!IsPause(points[i - 1], points[i]))
            {
                distance += GeoMath.HaversineMeters(points[i - 1].Coordinates, points[i].Coordinates);
            }
        }
        
        return (float)distance;
    }

    private static TimeSpan GetTotalTime(IReadOnlyList<TrackPoint> points)
    {
        return points[^1].Time -  points[0].Time;
    }

    private static TimeSpan GetActivityTime(IReadOnlyList<TrackPoint> points)
    {
        TimeSpan time = TimeSpan.Zero;
        for (int i = 1; i < points.Count; i++)
        {
            if (!IsPause(points[i - 1], points[i]))
            {
                time += points[i].Time - points[i - 1].Time;
            }
        }
        return time;
    }
    
    
    private static int? GetAvgHeartRate(IReadOnlyList<TrackPoint> points)
    {
        return (int?)points.Average(p => p.HeartRate);
    }
    
    private static int? GetMaxHeartRate(IReadOnlyList<TrackPoint> points)
    {
        return points.Max(p => p.HeartRate);
    }
    
    
    private static bool IsPause(TrackPoint point1, TrackPoint point2, float speedThresholdMetersPerSecond = 0.5f)
    {
        double distance = GeoMath.HaversineMeters(point1.Coordinates, point2.Coordinates);
        double seconds = (point2.Time - point1.Time).TotalSeconds;

        if (seconds <= 0) return true;
        if (seconds > MaxRecordingGap) return true;
        if (distance < speedThresholdMetersPerSecond * seconds) return true;
        
        return false;
    }
}