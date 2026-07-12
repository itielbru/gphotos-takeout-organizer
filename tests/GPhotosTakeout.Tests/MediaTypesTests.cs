using GPhotosTakeout.Core.Matching;
using Xunit;

namespace GPhotosTakeout.Tests;

public class MediaTypesTests
{
    [Theory]
    [InlineData("IMG_1234.jpg")]
    [InlineData("IMG_1234.JPG")]
    [InlineData("photo.jpeg")]
    [InlineData("photo.heic")]
    [InlineData("photo.webp")]
    [InlineData("photo.avif")]
    [InlineData("raw.dng")]
    [InlineData("raw.CR2")]
    [InlineData("clip.mp4")]
    [InlineData("clip.MOV")]
    [InlineData("clip.m2ts")]
    [InlineData("MVIMG_1234.mp")] // Pixel motion-photo video companion
    public void IsMedia_KnownPhotoAndVideoExtensions_True(string name) =>
        Assert.True(MediaTypes.IsMedia(name));

    [Theory]
    [InlineData("Microsoft.ui.xaml.dll")]
    [InlineData("Microsoft.ui.xaml.dll.mui")]
    [InlineData("GPhotosTakeout.App.exe")]
    [InlineData("Exif.pm")]           // ExifTool Perl module
    [InlineData("WritePDF.pl")]
    [InlineData("resources.pri")]
    [InlineData("app.winmd")]
    [InlineData("notes.txt")]
    [InlineData("archive.zip")]
    [InlineData("README")]            // no extension
    [InlineData("IMG_1234.jpg.supplemental-metadata.json")]
    public void IsMedia_NonMediaFiles_False(string name) =>
        Assert.False(MediaTypes.IsMedia(name));
}
