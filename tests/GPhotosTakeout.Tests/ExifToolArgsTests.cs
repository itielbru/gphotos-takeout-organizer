using GPhotosTakeout.Core.Metadata;
using Xunit;

namespace GPhotosTakeout.Tests;

public class ExifToolArgsTests
{
    [Fact]
    public void BuildArgs_Image_WritesDateTimeOriginalAndOffset()
    {
        var meta = new ExifMetadata
        {
            DateTakenLocal = new DateTime(2023, 8, 15, 17, 5, 36),
            Offset = "+03:00",
            Latitude = 32.0853,
            Longitude = 34.7818,
        };

        var args = ExifToolBatchWriter.BuildArgs(@"C:\out\IMG.jpg", meta);

        Assert.Contains("-EXIF:DateTimeOriginal=2023:08:15 17:05:36", args);
        Assert.Contains("-EXIF:OffsetTimeOriginal=+03:00", args);
        Assert.Contains("-EXIF:GPSLatitudeRef=N", args);
        Assert.Contains("-overwrite_original", args);
        Assert.Contains("filename=utf8", args);
        Assert.Equal(@"C:\out\IMG.jpg", args[^1]); // file path is last
    }

    [Fact]
    public void BuildArgs_Video_UsesQuickTimeUtcTags_NotExif()
    {
        var meta = new ExifMetadata
        {
            DateTakenUtc = new DateTime(2023, 8, 15, 14, 5, 36),
            Latitude = 32.0853,
            Longitude = 34.7818,
        };

        var args = ExifToolBatchWriter.BuildArgs(@"C:\out\VID.mp4", meta);

        Assert.Contains("-QuickTime:CreateDate=2023:08:15 14:05:36", args);
        Assert.Contains("QuickTimeUTC=1", args);
        Assert.DoesNotContain(args, a => a.StartsWith("-EXIF:DateTimeOriginal"));
        Assert.Contains(args, a => a.StartsWith("-Keys:GPSCoordinates="));
    }

    [Fact]
    public void BuildArgs_Favorited_WritesRating()
    {
        var meta = new ExifMetadata { Favorited = true };
        var args = ExifToolBatchWriter.BuildArgs(@"C:\out\IMG.jpg", meta);
        Assert.Contains("-XMP:Rating=5", args);
    }
}
