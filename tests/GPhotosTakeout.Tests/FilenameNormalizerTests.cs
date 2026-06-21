using GPhotosTakeout.Core.Matching;
using Xunit;

namespace GPhotosTakeout.Tests;

public class FilenameNormalizerTests
{
    [Theory]
    // Old format
    [InlineData("IMG_1234.jpg.json", "IMG_1234.jpg")]
    // New 2024+ format
    [InlineData("IMG_1234.jpg.supplemental-metadata.json", "IMG_1234.jpg")]
    // Truncated supplemental token (the Issue #353 cases)
    [InlineData("IMG_1234.jpg.supplemental-metad.json", "IMG_1234.jpg")]
    [InlineData("IMG_1234.jpg.supplem.json", "IMG_1234.jpg")]
    [InlineData("IMG_1234.jpg.s.json", "IMG_1234.jpg")]
    // No extension in sidecar name
    [InlineData("IMG_1234.supplemental-metadata.json", "IMG_1234")]
    [InlineData("IMG_1234.json", "IMG_1234")]
    public void RecoverMediaNameFromSidecar_HandlesAllNamingForms(string sidecar, string expected)
    {
        Assert.Equal(expected, FilenameNormalizer.RecoverMediaNameFromSidecar(sidecar));
    }

    [Fact]
    public void RecoverMediaNameFromSidecar_DoesNotEatRealExtension()
    {
        // ".jpg" is not a prefix of "supplemental-metadata" and must survive.
        Assert.Equal("photo.jpg", FilenameNormalizer.RecoverMediaNameFromSidecar("photo.jpg.json"));
    }

    [Theory]
    [InlineData("IMG_1234.jpg", "img_1234")]
    [InlineData("IMG_1234.HEIC", "img_1234")]
    [InlineData("IMG_1234-edited.jpg", "img_1234")]
    [InlineData("IMG_1234(1).jpg", "img_1234")]
    [InlineData("IMG_1234-edited(1).jpg", "img_1234")] // order: counter then edited
    [InlineData("IMG_1234", "img_1234")]
    public void NormalizeToKey_CollapsesVariantsToSameKey(string fileName, string expected)
    {
        Assert.Equal(expected, FilenameNormalizer.NormalizeToKey(fileName));
    }

    [Fact]
    public void NormalizeToKey_MediaAndRecoveredSidecar_Collide()
    {
        var media = FilenameNormalizer.NormalizeToKey("IMG_1234.jpg");
        var recovered = FilenameNormalizer.RecoverMediaNameFromSidecar("IMG_1234.jpg.supplem.json");
        var sidecarKey = FilenameNormalizer.NormalizeToKey(recovered);
        Assert.Equal(media, sidecarKey);
    }

    [Theory]
    [InlineData("IMG.jpg", false)]
    [InlineData("IMG.jpg.json", true)]
    [InlineData("IMG.jpg.supplemental-metadata.json", true)]
    public void IsSidecar_DetectsJson(string fileName, bool expected)
    {
        Assert.Equal(expected, FilenameNormalizer.IsSidecar(fileName));
    }
}
