namespace Cyclo.Core.Tests;

public class GeoMathTests
{
    // Expected values are derived from the spherical Earth model, not copied from a calculator.
    // Math.PI is a const, so these can be used inside [InlineData].
    private const double EarthRadiusMeters = 6_371_000;
    private const double OneDegreeArcMeters = 2 * Math.PI * EarthRadiusMeters / 360; // ~111 194.93 m
    private const double HalfCircumferenceMeters = Math.PI * EarthRadiusMeters;     // ~20 015 086.8 m

    [Theory]
    [InlineData(0, 0)]
    [InlineData(52.2297, 21.0122)]
    [InlineData(-33.8688, 151.2093)]
    public void HaversineMeters_SamePoint_ReturnsZero(double lat, double lon)
    {
        var point = new Coordinates(Lat: lat, Lon: lon);

        double result = GeoMath.HaversineMeters(point, point);

        Assert.Equal(0, result, tolerance: 1e-6);
    }

    [Fact]
    public void HaversineMeters_OneDegreeAlongEquator_ReturnsOneDegreeArc()
    {
        var p1 = new Coordinates(Lat: 0, Lon: 0);
        var p2 = new Coordinates(Lat: 0, Lon: 1);

        double result = GeoMath.HaversineMeters(p1, p2);

        Assert.Equal(OneDegreeArcMeters, result, tolerance: 0.01);
    }

    // Moves along latitude, so it exercises the delta-lat term that the equator case never touches.
    // Along a meridian 1° is the same arc at every latitude.
    [Theory]
    [InlineData(0)]
    [InlineData(50)]
    [InlineData(-89)]
    public void HaversineMeters_OneDegreeAlongMeridian_ReturnsOneDegreeArc(double startLat)
    {
        var p1 = new Coordinates(Lat: startLat, Lon: 20);
        var p2 = new Coordinates(Lat: startLat + 1, Lon: 20);

        double result = GeoMath.HaversineMeters(p1, p2);

        Assert.Equal(OneDegreeArcMeters, result, tolerance: 0.01);
    }

    // At 60° cos(lat) = 0.5, so 1° of longitude is roughly half of the equator value
    // (the great circle is ~0.5 m shorter than the parallel, hence the 1 m tolerance).
    // Fails if cos(lat) is missing or if Lat and Lon are swapped anywhere in the formula.
    [Fact]
    public void HaversineMeters_OneDegreeLongitudeAt60Lat_ReturnsAboutHalfOfEquatorDegree()
    {
        var p1 = new Coordinates(Lat: 60, Lon: 0);
        var p2 = new Coordinates(Lat: 60, Lon: 1);

        double result = GeoMath.HaversineMeters(p1, p2);

        Assert.Equal(OneDegreeArcMeters / 2, result, tolerance: 1);
    }

    // 179° -> -179° is 2° apart, not 358°.
    [Fact]
    public void HaversineMeters_AcrossAntimeridian_TakesShortWay()
    {
        var p1 = new Coordinates(Lat: 0, Lon: 179);
        var p2 = new Coordinates(Lat: 0, Lon: -179);

        double result = GeoMath.HaversineMeters(p1, p2);

        Assert.Equal(2 * OneDegreeArcMeters, result, tolerance: 0.01);
    }

    // Edge of the formula: q is ~1 here, and rounding can push it slightly above 1,
    // which makes Math.Asin return NaN.
    [Theory]
    [InlineData(0, 0, 0, 180)]
    [InlineData(90, 0, -90, 0)]
    [InlineData(45, 30, -45, -150)]
    public void HaversineMeters_AntipodalPoints_ReturnsHalfCircumference(
        double lat1, double lon1, double lat2, double lon2)
    {
        var p1 = new Coordinates(Lat: lat1, Lon: lon1);
        var p2 = new Coordinates(Lat: lat2, Lon: lon2);

        double result = GeoMath.HaversineMeters(p1, p2);

        Assert.Equal(HalfCircumferenceMeters, result, tolerance: 1);
    }

    // Real-world sanity check against a commonly quoted straight-line distance (~252 km).
    // The tolerance is loose on purpose: city "centre" coordinates differ between sources.
    [Fact]
    public void HaversineMeters_WarsawToKrakow_ReturnsAbout252Km()
    {
        var warsaw = new Coordinates(Lat: 52.2297, Lon: 21.0122);
        var krakow = new Coordinates(Lat: 50.0647, Lon: 19.9450);

        double result = GeoMath.HaversineMeters(warsaw, krakow);

        Assert.Equal(252_000, result, tolerance: 1_000);
    }

    // Typical distance between two GPS samples (1 s apart at ~40 km/h).
    // The method will be called with steps like this millions of times, so precision matters most here.
    [Fact]
    public void HaversineMeters_TypicalGpsStep_IsAccurateToCentimetres()
    {
        var p1 = new Coordinates(Lat: 52.0, Lon: 21.0);
        var p2 = new Coordinates(Lat: 52.0001, Lon: 21.0);

        double result = GeoMath.HaversineMeters(p1, p2);

        Assert.Equal(OneDegreeArcMeters * 0.0001, result, tolerance: 0.01); // ~11.12 m
    }

    // A property test: no expected number, only a rule that must hold for any pair.
    [Theory]
    [InlineData(52.2297, 21.0122, 50.0647, 19.9450)]
    [InlineData(-33.8688, 151.2093, 40.7128, -74.0060)]
    [InlineData(0, 179, 0, -179)]
    public void HaversineMeters_SwappedArguments_ReturnsSameDistance(
        double lat1, double lon1, double lat2, double lon2)
    {
        var a = new Coordinates(Lat: lat1, Lon: lon1);
        var b = new Coordinates(Lat: lat2, Lon: lon2);

        double ab = GeoMath.HaversineMeters(a, b);
        double ba = GeoMath.HaversineMeters(b, a);

        Assert.Equal(ab, ba, tolerance: 1e-6);
    }
}
