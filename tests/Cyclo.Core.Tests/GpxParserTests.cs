using System.Globalization;
using System.Text;
using System.Xml;
using Cyclo.Core.Models;
using Cyclo.Core.Parsing;

namespace Cyclo.Core.Tests;

public class GpxParserTests
{
    private const string GarminTpxNamespace = "http://www.garmin.com/xmlschemas/TrackPointExtension/v1";

    [Fact]
    public void Parse_TwoPoints_ReturnsPointsWithCoordinatesAndTime()
    {
        string gpx = WrapInGpx("""
            <trkpt lat="50.5" lon="17.25">
              <time>2026-08-09T08:15:11Z</time>
            </trkpt>
            <trkpt lat="50.6" lon="17.35">
              <time>2026-08-09T08:15:12Z</time>
            </trkpt>
            """);

        IReadOnlyList<TrackPoint> result = GpxParser.ParseTrackPoints(ToStream(gpx));

        Assert.Equal(2, result.Count);
        Assert.Equal(50.5, result[0].Coordinates.Lat);
        Assert.Equal(17.25, result[0].Coordinates.Lon);
        Assert.Equal(new DateTimeOffset(2026, 8, 9, 8, 15, 11, TimeSpan.Zero), result[0].Time);
        Assert.Equal(new DateTimeOffset(2026, 8, 9, 8, 15, 12, TimeSpan.Zero), result[1].Time);
    }

    // Fragment copied from a real Garmin Connect export.
    [Fact]
    public void Parse_PointWithGarminExtensions_ReadsElevationTemperatureAndHeartRate()
    {
        string gpx = WrapInGpx("""
            <trkpt lat="50.5" lon="17.25">
              <ele>133</ele>
              <time>2026-08-09T08:15:11.000Z</time>
              <extensions>
                <ns3:TrackPointExtension>
                  <ns3:atemp>29.0</ns3:atemp>
                  <ns3:hr>120</ns3:hr>
                </ns3:TrackPointExtension>
              </extensions>
            </trkpt>
            """, extraNamespaces: $"""xmlns:ns3="{GarminTpxNamespace}" """);

        TrackPoint point = Assert.Single(GpxParser.ParseTrackPoints(ToStream(gpx)));

        Assert.Equal(133f, point.Elevation);
        Assert.Equal(29f, point.Temperature);
        Assert.Equal((byte)120, point.HeartRate);
    }

    // The prefix is just a local alias – only the namespace URI matters.
    [Fact]
    public void Parse_ExtensionsWithDifferentPrefix_StillReadsHeartRate()
    {
        string gpx = WrapInGpx("""
            <trkpt lat="50.5" lon="17.25">
              <time>2026-08-09T08:15:11Z</time>
              <extensions>
                <gpxtpx:TrackPointExtension>
                  <gpxtpx:hr>140</gpxtpx:hr>
                </gpxtpx:TrackPointExtension>
              </extensions>
            </trkpt>
            """, extraNamespaces: $"""xmlns:gpxtpx="{GarminTpxNamespace}" """);

        TrackPoint point = Assert.Single(GpxParser.ParseTrackPoints(ToStream(gpx)));

        Assert.Equal((byte)140, point.HeartRate);
    }

    // Garmin writes elevation through float, e.g. 150.600006103515625 – it must round-trip to 150.6f.
    [Fact]
    public void Parse_ElevationWithFloatArtifact_ReturnsExpectedFloat()
    {
        string gpx = WrapInGpx("""
            <trkpt lat="50.5" lon="17.25">
              <ele>150.600006103515625</ele>
              <time>2026-08-09T08:15:11Z</time>
            </trkpt>
            """);

        TrackPoint point = Assert.Single(GpxParser.ParseTrackPoints(ToStream(gpx)));

        Assert.Equal(150.6f, point.Elevation);
    }

    [Fact]
    public void Parse_PointWithoutOptionalElements_ReturnsNullsForOptionalFields()
    {
        string gpx = WrapInGpx("""
            <trkpt lat="50.5" lon="17.25">
              <time>2026-08-09T08:15:11Z</time>
            </trkpt>
            """);

        TrackPoint point = Assert.Single(GpxParser.ParseTrackPoints(ToStream(gpx)));

        Assert.Null(point.Elevation);
        Assert.Null(point.Temperature);
        Assert.Null(point.HeartRate);
    }

    // Extensions present, but without heart rate (e.g. no chest strap) – must not throw.
    [Fact]
    public void Parse_ExtensionsWithoutHeartRate_ReturnsNullHeartRate()
    {
        string gpx = WrapInGpx("""
            <trkpt lat="50.5" lon="17.25">
              <time>2026-08-09T08:15:11Z</time>
              <extensions>
                <ns3:TrackPointExtension>
                  <ns3:atemp>21.0</ns3:atemp>
                </ns3:TrackPointExtension>
              </extensions>
            </trkpt>
            """, extraNamespaces: $"""xmlns:ns3="{GarminTpxNamespace}" """);

        TrackPoint point = Assert.Single(GpxParser.ParseTrackPoints(ToStream(gpx)));

        Assert.Equal(21f, point.Temperature);
        Assert.Null(point.HeartRate);
    }

    // A point missing any mandatory value is skipped; its neighbours are kept.
    [Theory]
    [InlineData("""<trkpt lon="17.3"><time>2026-08-09T08:15:12Z</time></trkpt>""")]
    [InlineData("""<trkpt lat="50.6"><time>2026-08-09T08:15:12Z</time></trkpt>""")]
    [InlineData("""<trkpt lat="50.6" lon="17.3"></trkpt>""")]
    public void Parse_PointMissingMandatoryValue_IsSkipped(string brokenPoint)
    {
        string gpx = WrapInGpx($"""
            <trkpt lat="50.5" lon="17.25"><time>2026-08-09T08:15:11Z</time></trkpt>
            {brokenPoint}
            <trkpt lat="50.7" lon="17.35"><time>2026-08-09T08:15:13Z</time></trkpt>
            """);

        IReadOnlyList<TrackPoint> result = GpxParser.ParseTrackPoints(ToStream(gpx));

        Assert.Equal(2, result.Count);
        Assert.Equal(50.5, result[0].Coordinates.Lat);
        Assert.Equal(50.7, result[1].Coordinates.Lat);
    }

    // Garmin pauses do not split segments, but other apps do – points from all segments count.
    [Fact]
    public void Parse_MultipleSegments_ReturnsPointsFromAllSegments()
    {
        const string gpx = """
            <?xml version="1.0" encoding="UTF-8"?>
            <gpx version="1.1" creator="test" xmlns="http://www.topografix.com/GPX/1/1">
              <trk>
                <trkseg>
                  <trkpt lat="50.5" lon="17.25"><time>2026-08-09T08:15:11Z</time></trkpt>
                </trkseg>
                <trkseg>
                  <trkpt lat="50.6" lon="17.35"><time>2026-08-09T09:00:00Z</time></trkpt>
                </trkseg>
              </trk>
            </gpx>
            """;

        IReadOnlyList<TrackPoint> result = GpxParser.ParseTrackPoints(ToStream(gpx));

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void Parse_TrackWithoutPoints_ReturnsEmptyList()
    {
        string gpx = WrapInGpx("");

        IReadOnlyList<TrackPoint> result = GpxParser.ParseTrackPoints(ToStream(gpx));

        Assert.Empty(result);
    }

    // A corrupted upload must fail loudly, not look like an empty ride.
    [Fact]
    public void Parse_TruncatedXml_ThrowsInvalidGpxWithXmlInner()
    {
        const string truncated = """
            <?xml version="1.0" encoding="UTF-8"?>
            <gpx version="1.1" creator="test" xmlns="http://www.topografix.com/GPX/1/1">
              <trk>
                <trkseg>
                  <trkpt lat="50.5" lon="17.25"><time>2026-08-09T08:15:11Z</time></trkpt>
                  <trkpt lat="50.6" lo
            """;

        var ex = Assert.Throws<InvalidGpxException>(() => GpxParser.ParseTrackPoints(ToStream(truncated)));

        Assert.IsType<XmlException>(ex.InnerException);
        Assert.Contains("line", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    // DtdProcessing.Prohibit is the XXE guard – a file with a DTD must be rejected, not expanded.
    [Fact]
    public void Parse_FileWithDtd_ThrowsInvalidGpx()
    {
        const string withDtd = """
            <?xml version="1.0" encoding="UTF-8"?>
            <!DOCTYPE gpx [ <!ENTITY xxe SYSTEM "file:///c:/windows/win.ini"> ]>
            <gpx version="1.1" creator="test" xmlns="http://www.topografix.com/GPX/1/1">
              <trk><trkseg><trkpt lat="50.5" lon="17.25"><time>&xxe;</time></trkpt></trkseg></trk>
            </gpx>
            """;

        var ex = Assert.Throws<InvalidGpxException>(() => GpxParser.ParseTrackPoints(ToStream(withDtd)));

        Assert.IsType<XmlException>(ex.InnerException);
    }

    // Well-formed XML, but a value that cannot be converted – a broken file, not a skipped point.
    [Theory]
    [InlineData("""<trkpt lat="abc" lon="17.25"><time>2026-08-09T08:15:11Z</time></trkpt>""")]
    [InlineData("""<trkpt lat="50.5" lon="17.25"><time>yesterday</time></trkpt>""")]
    [InlineData("""<trkpt lat="50.5" lon="17.25"><ele>high</ele><time>2026-08-09T08:15:11Z</time></trkpt>""")]
    public void Parse_InvalidValue_ThrowsInvalidGpxWithFormatInner(string brokenPoint)
    {
        string gpx = WrapInGpx(brokenPoint);

        var ex = Assert.Throws<InvalidGpxException>(() => GpxParser.ParseTrackPoints(ToStream(gpx)));

        Assert.IsType<FormatException>(ex.InnerException);
    }

    [Fact]
    public void Parse_HeartRateOutOfIntRange_ThrowsInvalidGpxWithOverflowInner()
    {
        string gpx = WrapInGpx("""
            <trkpt lat="50.5" lon="17.25">
              <time>2026-08-09T08:15:11Z</time>
              <extensions>
                <ns3:TrackPointExtension>
                  <ns3:hr>99999999999</ns3:hr>
                </ns3:TrackPointExtension>
              </extensions>
            </trkpt>
            """, extraNamespaces: $"""xmlns:ns3="{GarminTpxNamespace}" """);

        var ex = Assert.Throws<InvalidGpxException>(() => GpxParser.ParseTrackPoints(ToStream(gpx)));

        Assert.IsType<OverflowException>(ex.InnerException);
    }

    // On a Polish Windows the decimal separator is a comma; parsing must not depend on that.
    [Fact]
    public void Parse_UnderPolishCulture_ParsesDecimalPoint()
    {
        string gpx = WrapInGpx("""
            <trkpt lat="50.5" lon="17.25">
              <ele>133.4</ele>
              <time>2026-08-09T08:15:11Z</time>
            </trkpt>
            """);

        CultureInfo originalCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("pl-PL");

            TrackPoint point = Assert.Single(GpxParser.ParseTrackPoints(ToStream(gpx)));

            Assert.Equal(50.5, point.Coordinates.Lat);
            Assert.Equal(133.4f, point.Elevation);
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }
    }

    private static Stream ToStream(string xml) => new MemoryStream(Encoding.UTF8.GetBytes(xml));

    private static string WrapInGpx(string trackPoints, string extraNamespaces = "") => $"""
        <?xml version="1.0" encoding="UTF-8"?>
        <gpx version="1.1" creator="test" xmlns="http://www.topografix.com/GPX/1/1" {extraNamespaces}>
          <trk>
            <trkseg>
        {trackPoints}
            </trkseg>
          </trk>
        </gpx>
        """;
}
