namespace GPhotosTakeout.Core.Matching;

/// <summary>
/// Single owner of "what counts as a photo or video". The pipeline only organizes
/// and writes metadata into files with these extensions; anything else found in the
/// input (documents, executables, stray application files) is counted and skipped.
/// Without this filter, pointing the app at a folder containing arbitrary files
/// (e.g. its own installation directory) organized DLLs into the photo library and
/// fed them to ExifTool, which rejected every one.
/// </summary>
public static class MediaTypes
{
    private static readonly HashSet<string> Extensions = new(StringComparer.OrdinalIgnoreCase)
    {
        // Photos
        ".jpg", ".jpeg", ".jfif", ".png", ".gif", ".bmp", ".webp",
        ".heic", ".heif", ".tif", ".tiff", ".avif",
        // Camera raw (Google Photos accepts most raw formats)
        ".dng", ".arw", ".cr2", ".cr3", ".crw", ".nef", ".nrw", ".orf",
        ".raf", ".rw2", ".pef", ".srw", ".x3f", ".erf", ".kdc", ".mef",
        ".mos", ".mrw", ".sr2", ".srf", ".dcr", ".raw", ".3fr",
        // Videos
        ".mp4", ".mov", ".m4v", ".avi", ".wmv", ".mkv", ".webm",
        ".3gp", ".3g2", ".mpg", ".mpeg", ".m2ts", ".mts", ".ts",
        ".vob", ".divx", ".flv", ".m2t", ".mmv", ".tod", ".dv",
        ".f4v", ".lrv",
        // Pixel motion-photo video companion
        ".mp",
    };

    public static bool IsMedia(string fileName) =>
        Extensions.Contains(Path.GetExtension(fileName));
}
