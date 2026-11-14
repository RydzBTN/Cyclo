using System.Xml;
using System.Xml.Linq;
using Cyclo.Core.Models;

namespace Cyclo.Core.Parsing;

public static class GpxParser
{
    private static readonly XNamespace garmin = "http://www.garmin.com/xmlschemas/TrackPointExtension/v1";
    private static readonly XNamespace gpx = "http://www.topografix.com/GPX/1/1";
    
    /// <summary>
    /// 
    /// </summary>
    /// <param name="stream"></param>
    /// <returns></returns>
    /// <exception cref="InvalidGpxException"></exception>
    public static IReadOnlyList<TrackPoint> ParseTrackPoints(Stream stream)
    {
        List<TrackPoint> points = new List<TrackPoint>();

        try
        {
            XmlReaderSettings settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit };

            using XmlReader reader = XmlReader.Create(stream, settings);
            XDocument doc = XDocument.Load(reader);

            foreach (XElement docPoint in doc.Descendants(gpx + "trkpt"))
            {
                if (!TryParseTrackPoint(docPoint, out TrackPoint point)) continue;
                points.Add(point);
            }
        }
        catch (XmlException ex)
        {
            if (ex.LineNumber == 0) 
                throw new InvalidGpxException($"Invalid XML", ex);
            else 
                throw new InvalidGpxException($"Invalid XML at line {ex.LineNumber}", ex);
            
        }
        catch (Exception ex) when (ex is FormatException or OverflowException)
        {
            throw new InvalidGpxException("File contains invalid track point value", ex);
        }
        
        return points;
    }

    private static bool TryParseTrackPoint(XElement xmlPoint, out TrackPoint point)
    {
        point = default;
        
        double? lat = (double?)xmlPoint.Attribute("lat");
        double? lon = (double?)xmlPoint.Attribute("lon");
        DateTimeOffset? time = (DateTimeOffset?)xmlPoint.Element(gpx + "time");
        
        if (lat == null || lon == null || time == null) return false;

        float? elevation = (float?)xmlPoint.Element(gpx + "ele");
        float? temperature = (float?)xmlPoint.Element(gpx + "extensions")
            ?.Element(garmin + "TrackPointExtension")?.Element(garmin + "atemp");
        int? heartRate = (int?)xmlPoint.Element(gpx + "extensions")?.Element(garmin + "TrackPointExtension")
            ?.Element(garmin + "hr");

        point = new TrackPoint(new Coordinates(lat.Value, lon.Value), time.Value, elevation,
            temperature, heartRate);

        return true;
    }
}