namespace Cyclo.Core;

public static class ActivityAnalyzer
{
    private const int MaxRecordingGap = 60;
    private const float ElevationThreshold = 1.1f; // tested in comparison to real garmin summary
    
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
            GetMaxHeartRate(points),
            GetElevation(points),
            activityDistance >= 1000 ? GetBestSegmentFast(points, 1000) : null,
            activityDistance >= 5000 ? GetBestSegmentFast(points, 5000) : null,
            activityDistance >= 10_000 ? GetBestSegmentFast(points, 10_000) : null
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

    private static float? GetElevation(IReadOnlyList<TrackPoint> points)
    {
        float? firstElevation = points.Select(p => p.Elevation).FirstOrDefault(ele => ele.HasValue);
        if(firstElevation is not float anchor) return null;
        float elevation = 0;
        for (int i = 1; i < points.Count; i++)
        {
            float? point = points[i].Elevation;
            
            if (!IsPause(points[i - 1], points[i]) && point.HasValue)
            {
                if(Math.Abs(point.Value - anchor) < ElevationThreshold) 
                    continue;
                if (point.Value - anchor >= ElevationThreshold) // up
                {
                    elevation += point.Value - anchor;
                    anchor = point.Value;
                }
                else if (anchor - point.Value >= ElevationThreshold) // down
                {
                    anchor = point.Value;
                }
            }
            else  // pause
            {
                if(point.HasValue)anchor = point.Value;
            }
        }
        
        return (float)Math.Round(elevation);
    }

    private static BestSegment? GetBestSegment(IReadOnlyList<TrackPoint> points, double distance) // O(n²) version
    {
        bool foundSplit = false;
        TimeSpan bestTime = TimeSpan.MaxValue;
        double bestDistance = float.MinValue;
        TrackPoint bestStart = points[0];
        TrackPoint bestEnd = points[^1];
        
        for (int i = 0; i < points.Count; i++)
        {
            double currentDistance = 0.0;
            
            for (int j = (i + 1); j < points.Count; j++)
            {
                currentDistance += GeoMath.HaversineMeters(points[j - 1].Coordinates, points[j].Coordinates);
                
                if(IsPause(points[j - 1],  points[j], 0.0f)) // There is gap longer than 60s in segment
                    break; 
                
                
                // TODO condition based on m/s not distance like in GetBestSegmentFast
                if (currentDistance >= distance)
                {
                    TimeSpan time = points[j].Time - points[i].Time;
                   
                    if (time < bestTime)
                    {
                        bestDistance = currentDistance;
                        bestStart = points[i];
                        bestEnd = points[j];
                        bestTime = time;
                        foundSplit = true;
                    }
                    
                    break;
                }
            }
        }

        if (!foundSplit) return null;
        return new BestSegment(bestStart.Time, bestEnd.Time, (float)(bestDistance / bestTime.TotalSeconds));
    }

    private static BestSegment? GetBestSegmentFast(IReadOnlyList<TrackPoint> points, double distance)
    {
        double[] cumulated =  new double[points.Count];
        cumulated[0] = 0.0;
        
        for (int i = 1; i < points.Count; i++)
        {
            cumulated[i] = GeoMath.HaversineMeters(points[i - 1].Coordinates, points[i].Coordinates) + cumulated[i - 1];
        }
        
        bool foundSplit = false;
        float bestSpeed = 0f;
        int bestStart = 0;
        int bestEnd = 1;
        
        int segStart = 0, segEnd = 1;
        for (segEnd = 1; segEnd < points.Count; segEnd++)
        {
            if (IsPause(points[segEnd - 1], points[segEnd], 0.0f))
            {
                segStart = segEnd;
                continue;
            }
            
            while (cumulated[segEnd] - cumulated[segStart + 1] >= distance) segStart++;
            
            if (cumulated[segEnd] - cumulated[segStart] >= distance)
            {
                float speed = (float)((cumulated[segEnd] - cumulated[segStart]) /
                                      (points[segEnd].Time - points[segStart].Time).TotalSeconds);
                    
                if (speed > bestSpeed)
                {
                    bestStart = segStart;
                    bestEnd = segEnd;
                    bestSpeed = speed;
                    foundSplit = true;
                }
            }
        }
        
        if (!foundSplit) return null;
        
        return new BestSegment(
            points[bestStart].Time,
            points[bestEnd].Time,
            bestSpeed
            );
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