namespace GPhotosTakeout.Core.IO;

/// <summary>
/// Helpers for working with paths that may exceed the legacy MAX_PATH (260) limit.
/// Even with a longPathAware manifest, some BCL paths (and ZipFile.ExtractToFile)
/// still fail, so we prefix absolute paths with the extended-length "\\?\" form
/// and open streams manually.
/// </summary>
public static class LongPath
{
    /// <summary>
    /// Returns the extended-length form of an absolute path ("\\?\C:\very\long...").
    /// UNC paths become "\\?\UNC\server\share\...". Non-absolute paths are returned
    /// unchanged.
    /// </summary>
    public static string Extended(string path)
    {
        if (path.StartsWith(@"\\?\", StringComparison.Ordinal))
            return path;

        // The OS uses "\\?\" paths verbatim — it does NOT translate '/' to '\'. So any
        // forward slashes (common when a path comes from a CLI arg or config) must be
        // normalized before we add the prefix, or the file appears to not exist.
        if (path.StartsWith(@"\\", StringComparison.Ordinal) || path.StartsWith("//", StringComparison.Ordinal))
            return @"\\?\UNC\" + path.Replace('/', '\\')[2..];

        if (Path.IsPathFullyQualified(path))
            return @"\\?\" + path.Replace('/', '\\');

        return path;
    }

    /// <summary>Creates all directories in the path, tolerating long paths.</summary>
    public static void EnsureDirectory(string directory) =>
        Directory.CreateDirectory(Extended(directory));

    /// <summary>Opens a file for reading, tolerating long paths.</summary>
    public static FileStream OpenRead(string path) =>
        new(Extended(path), FileMode.Open, FileAccess.Read, FileShare.Read, 1 << 16, useAsync: true);

    /// <summary>Creates/overwrites a file for writing, creating parent dirs, tolerating long paths.</summary>
    public static FileStream Create(string path)
    {
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir))
            EnsureDirectory(dir);
        return new FileStream(Extended(path), FileMode.Create, FileAccess.Write, FileShare.None, 1 << 16, useAsync: true);
    }

    public static bool Exists(string path) => File.Exists(Extended(path));

    /// <summary>Moves a file, creating the destination directory, tolerating long paths.</summary>
    public static void Move(string source, string destination, bool overwrite = false)
    {
        var dir = Path.GetDirectoryName(destination);
        if (!string.IsNullOrEmpty(dir))
            EnsureDirectory(dir);
        File.Move(Extended(source), Extended(destination), overwrite);
    }

    public static void Delete(string path) => File.Delete(Extended(path));
}
