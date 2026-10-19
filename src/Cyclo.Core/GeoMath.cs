namespace Cyclo.Core;

public static class GeoMath
{
    public static double HaversineMeters(Coordinates p1, Coordinates p2)
    {
        const double r = 6_371_000; // earth avg
        
        double lat1 = double.DegreesToRadians(p1.Lat);
        double lat2 = double.DegreesToRadians(p2.Lat);
        double lon1 = double.DegreesToRadians(p1.Lon);
        double lon2 = double.DegreesToRadians(p2.Lon);
        
        var sdlat = Math.Sin((lat2 - lat1) / 2);
        var sdlon = Math.Sin((lon2 - lon1) / 2);
        var q = sdlat * sdlat + Math.Cos(lat1) * Math.Cos(lat2) * sdlon * sdlon;
        var d = 2 * r * Math.Asin(Math.Sqrt(q));

        return d;
    }
}