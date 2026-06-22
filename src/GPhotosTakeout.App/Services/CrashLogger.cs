using System;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;

namespace GPhotosTakeout.App.Services;

/// <summary>
/// Writes a detailed, manually-reportable crash dump to the app's log folder.
/// There is no telemetry (see SECURITY.md) — this exists purely so a user can attach
/// the dump to a GitHub issue. Must never throw: it runs from the global exception handler.
/// </summary>
public static class CrashLogger
{
    public static string LogDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "GPhotosTakeout", "logs");

    /// <summary>Writes <paramref name="ex"/> to a timestamped crash file and returns its path
    /// (or null if even writing the dump failed).</summary>
    public static string? Write(Exception? ex)
    {
        try
        {
            Directory.CreateDirectory(LogDirectory);
            var path = Path.Combine(LogDirectory, $"crash-{DateTime.Now:yyyyMMdd-HHmmss}.log");
            var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "unknown";
            var ci = CultureInfo.InvariantCulture;
            var sb = new StringBuilder()
                .AppendLine("=== GPhotosTakeout crash report ===")
                .AppendLine(ci, $"Time:    {DateTimeOffset.Now:O}")
                .AppendLine(ci, $"Version: {version}")
                .AppendLine(ci, $"OS:      {Environment.OSVersion} ({(Environment.Is64BitProcess ? "x64" : "x86")})")
                .AppendLine(ci, $"Runtime: {Environment.Version}")
                .AppendLine()
                .AppendLine(ex?.ToString() ?? "(no exception object)");
            File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
            return path;
        }
        catch
        {
            return null;
        }
    }
}
