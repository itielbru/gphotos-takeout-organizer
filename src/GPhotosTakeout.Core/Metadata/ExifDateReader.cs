using GPhotosTakeout.Core.Dates;
using MetadataExtractor;
using MetadataExtractor.Formats.Exif;
using MetadataExtractor.Formats.QuickTime;

namespace GPhotosTakeout.Core.Metadata;

/// <summary>
/// Reads a capture date embedded in a media file, used as the EXIF fallback tier when
/// the Takeout sidecar has no usable date. Managed (MetadataExtractor), so it works
/// even when ExifTool isn't installed. Never throws — unreadable or dateless files
/// simply return null and the resolver falls through to the next tier.
/// </summary>
public static class ExifDateReader
{
    public static ExifDate? TryRead(string filePath)
    {
        try
        {
            using var stream = IO.LongPath.OpenRead(filePath);
            var directories = ImageMetadataReader.ReadMetadata(stream);

            // Images: DateTimeOriginal (capture), then DateTimeDigitized (scan time).
            // Both are naive local wall-clock values per the EXIF spec.
            foreach (var dir in directories.OfType<ExifSubIfdDirectory>())
            {
                if (dir.TryGetDateTime(ExifDirectoryBase.TagDateTimeOriginal, out var original) && IsPlausible(original))
                    return new ExifDate(original, IsUtc: false);
                if (dir.TryGetDateTime(ExifDirectoryBase.TagDateTimeDigitized, out var digitized) && IsPlausible(digitized))
                    return new ExifDate(digitized, IsUtc: false);
            }

            // Video (QuickTime/MP4): the movie-header creation time is a UTC instant.
            // Cameras that don't set it write the 1904 epoch zero — IsPlausible rejects it.
            foreach (var dir in directories.OfType<QuickTimeMovieHeaderDirectory>())
            {
                if (dir.TryGetDateTime(QuickTimeMovieHeaderDirectory.TagCreated, out var created) && IsPlausible(created))
                    return new ExifDate(created, IsUtc: true);
            }
        }
        catch (Exception ex) when (ex is ImageProcessingException or IOException or UnauthorizedAccessException)
        {
            // Corrupt/truncated/unsupported file: no EXIF tier for this one.
        }

        return null;
    }

    private static bool IsPlausible(DateTime value) => value.Year is >= 1971 and <= 2100;
}
