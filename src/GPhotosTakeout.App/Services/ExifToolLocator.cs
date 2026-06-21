using System;
using System.IO;

namespace GPhotosTakeout.App.Services;

/// <summary>
/// Finds the bundled exiftool.exe. Convention: a "Tools" folder next to the app
/// executable (the project ships ExifTool there). Returns null when absent so the
/// UI can warn that metadata writing is unavailable.
/// </summary>
public static class ExifToolLocator
{
    public static string? Find()
    {
        var baseDir = AppContext.BaseDirectory;
        var candidates = new[]
        {
            Path.Combine(baseDir, "Tools", "exiftool.exe"),
            Path.Combine(baseDir, "exiftool.exe"),
        };

        foreach (var c in candidates)
            if (File.Exists(c))
                return c;

        return null;
    }
}
