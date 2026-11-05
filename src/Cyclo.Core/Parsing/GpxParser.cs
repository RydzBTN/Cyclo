using System.Xml;
using System.Xml.Linq;
using Cyclo.Core.Models;

namespace Cyclo.Core.Parsing;

public static class GpxParser
{
    private static readonly XNamespace garmin = "http://www.garmin.com/xmlschemas/TrackPointExtension/v1";
    private static readonly XNamespace gpx = "http://www.topografix.com/GPX/1/1";
    
    public static IReadOnlyList<TrackPoint> ParseTrackPoints(Stream stream)
    {
        XmlReaderSettings settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit };
        using XmlReader reader = XmlReader.Create(stream, settings);
        
        XDocument doc = XDocument.Load(reader);
        
        List<TrackPoint> points = new List<TrackPoint>();
        
        foreach (XElement docPoint in doc.Descendants(gpx + "trkpt"))
        {
            double? lat = (double?)docPoint.Attribute("lat");
            double? lon = (double?)docPoint.Attribute("lon");
            DateTimeOffset? time = (DateTimeOffset?)docPoint.Element(gpx + "time");
            
            if(lat == null || lon == null || time == null) continue;
            
            float? elevation = (float?)docPoint.Element(gpx + "ele");
            float? temperature = (float?)docPoint.Element(gpx + "extensions")?.Element(garmin + "TrackPointExtension")?.Element(garmin + "atemp");
            int? heartRate = (int?)docPoint.Element(gpx + "extensions")?.Element(garmin + "TrackPointExtension")?.Element(garmin + "hr");
            
            TrackPoint point = new TrackPoint(new Coordinates(lat.Value, lon.Value), time.Value, elevation, temperature, heartRate);
            points.Add(point);
        }
        
        return points;
    }
}