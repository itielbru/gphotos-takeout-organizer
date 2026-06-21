using GPhotosTakeout.Core.Dates;
using GPhotosTakeout.Core.Models;
using Xunit;

namespace GPhotosTakeout.Tests;

public class DateResolverTests
{
    private readonly DateResolver _resolver = new();

    [Theory]
    [InlineData("IMG_20230815_142536.jpg", 2023, 8, 15, 14, 25, 36)]
    [InlineData("PXL_20230815_142536123.jpg", 2023, 8, 15, 14, 25, 36)]
    [InlineData("VID_20230815_142536.mp4", 2023, 8, 15, 14, 25, 36)]
    [InlineData("Screenshot_2023-08-15-14-25-36.png", 2023, 8, 15, 14, 25, 36)]
    public void TryFromFilename_ParsesCommonPatterns(string name, int y, int mo, int d, int h, int mi, int s)
    {
        Assert.True(DateResolver.TryFromFilename(name, out var value));
        Assert.Equal(new DateTime(y, mo, d, h, mi, s), value);
    }

    [Fact]
    public void TryFromFilename_WhatsApp_DateOnly()
    {
        Assert.True(DateResolver.TryFromFilename("IMG-20230815-WA0001.jpg", out var value));
        Assert.Equal(new DateTime(2023, 8, 15, 0, 0, 0), value);
    }

    [Fact]
    public void TryFromFilename_RejectsNonDate()
    {
        Assert.False(DateResolver.TryFromFilename("vacation.jpg", out _));
    }

    [Fact]
    public void Resolve_PrefersJsonOverFilename()
    {
        var json = TakeoutJson.Parse("""{"photoTakenTime":{"timestamp":"1692108336"}}""");
        var r = _resolver.Resolve("IMG_20000101_000000.jpg", json, null, null, null);
        Assert.Equal(DateSource.Json, r.Source);
        Assert.True(r.IsUtc);
        Assert.Equal(2023, r.Value.Year);
    }

    [Fact]
    public void Resolve_FallsBackToFilename_WhenNoJson()
    {
        var r = _resolver.Resolve("IMG_20230815_142536.jpg", null, null, null, null);
        Assert.Equal(DateSource.Filename, r.Source);
        Assert.False(r.IsUtc);
    }

    [Fact]
    public void Resolve_FallsBackToFolderYear()
    {
        var r = _resolver.Resolve("vacation.jpg", null, null, "Takeout/Google Photos/Photos from 2019", null);
        Assert.Equal(DateSource.AlbumFolder, r.Source);
        Assert.Equal(2019, r.Value.Year);
    }
}
