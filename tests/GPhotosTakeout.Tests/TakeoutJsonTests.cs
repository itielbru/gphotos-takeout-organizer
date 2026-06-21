using GPhotosTakeout.Core.Models;
using Xunit;

namespace GPhotosTakeout.Tests;

public class TakeoutJsonTests
{
    private const string Sample = """
    {
      "title": "IMG_1234.jpg",
      "description": "חופשה בים",
      "photoTakenTime": { "timestamp": "1692108336", "formatted": "Aug 15, 2023" },
      "geoData": { "latitude": 32.0853, "longitude": 34.7818, "altitude": 12.0 },
      "geoDataExif": { "latitude": 0.0, "longitude": 0.0, "altitude": 0.0 },
      "favorited": true
    }
    """;

    [Fact]
    public void Parse_ReadsAllFields_IncludingHebrew()
    {
        var json = TakeoutJson.Parse(Sample)!;
        Assert.Equal("IMG_1234.jpg", json.Title);
        Assert.Equal("חופשה בים", json.Description);
        Assert.True(json.Favorited);
    }

    [Fact]
    public void CapturedUtc_ConvertsEpoch()
    {
        var json = TakeoutJson.Parse(Sample)!;
        Assert.Equal(new DateTimeOffset(2023, 8, 15, 14, 5, 36, TimeSpan.Zero), json.CapturedUtc);
    }

    [Fact]
    public void BestGeo_PrefersPresentGeoData_IgnoresZeroExif()
    {
        var json = TakeoutJson.Parse(Sample)!;
        Assert.NotNull(json.BestGeo);
        Assert.Equal(32.0853, json.BestGeo!.Latitude, 4);
    }

    [Fact]
    public void BestGeo_NullWhenAllZero()
    {
        var json = TakeoutJson.Parse("""{"geoData":{"latitude":0,"longitude":0}}""")!;
        Assert.Null(json.BestGeo);
    }
}
