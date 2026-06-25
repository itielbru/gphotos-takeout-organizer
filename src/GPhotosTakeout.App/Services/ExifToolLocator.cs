using System;
using System.IO;

namespace GPhotosTakeout.App.Services;

/// <summary>
/// Finds exiftool.exe. Checks the per-user install folder first (where the one-click
/// installer writes it — see <see cref="ExifToolInstaller.TargetDir"/>), then a "Tools"
/// folder next to the executable for folder-based/dev installs. Returns null when absent
/// so the UI can warn that metadata writing is unavailable.
/// </summary>
public static class ExifToolLocator
{
    public static string? Find()
    {
        var baseDir = AppContext.BaseDirectory;
        var candidates = new[]
        {
            Path.Combine(ExifToolInstaller.TargetDir, "exiftool.exe"),
            Path.Combine(baseDir, "Tools", "exiftool.exe"),
            Path.Combine(baseDir, "exiftool.exe"),
        };

        foreach (var c in candidates)
            if (File.Exists(c))
                return c;

        return null;
    }
}
