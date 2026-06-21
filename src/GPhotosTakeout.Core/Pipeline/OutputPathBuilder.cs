using System.Globalization;
using GPhotosTakeout.Core.Dates;
using GPhotosTakeout.Core.Models;

namespace GPhotosTakeout.Core.Pipeline;

/// <summary>Classifies Takeout source folders into the special buckets Google uses.</summary>
public enum SpecialFolder { None, Archive, Trash, LockedFolder, PartnerShared }

/// <summary>
/// Builds the destination path for one media file according to the chosen output
/// structure, and recognizes Google's special folders so they can be segregated.
/// </summary>
public sealed class OutputPathBuilder
{
    public const string AllPhotos = "ALL_PHOTOS";

    private static readonly char[] PathSeparators = ['/', '\\'];

    private readonly OutputStructure _structure;

    public OutputPathBuilder(OutputStructure structure) => _structure = structure;

    /// <summary>
    /// Computes the absolute destination path under <paramref name="outputRoot"/>
    /// for a media file with the given resolved date.
    /// </summary>
    public string BuildPath(string outputRoot, TakeoutEntry media, DateTime? captureLocal)
    {
        var special = Classify(media.Folder);
        var fileName = media.FileName;

        if (special != SpecialFolder.None)
            return Path.Combine(outputRoot, special.ToString(), fileName);

        return _structure switch
        {
            OutputStructure.Flat => Path.Combine(outputRoot, AllPhotos, fileName),
            OutputStructure.Albums => Path.Combine(outputRoot, SanitizeAlbum(media.Folder), fileName),
            _ => BuildYearMonth(outputRoot, fileName, captureLocal),
        };
    }

    private static string BuildYearMonth(string outputRoot, string fileName, DateTime? date)
    {
        if (date is not { } d)
            return Path.Combine(outputRoot, AllPhotos, "Undated", fileName);

        var year = d.Year.ToString(CultureInfo.InvariantCulture);
        var month = d.ToString("yyyy-MM", CultureInfo.InvariantCulture);
        return Path.Combine(outputRoot, AllPhotos, year, month, fileName);
    }

    public static SpecialFolder Classify(string folder)
    {
        var name = LastSegment(folder);
        if (name.Equals("Archive", StringComparison.OrdinalIgnoreCase)) return SpecialFolder.Archive;
        if (name.Equals("Trash", StringComparison.OrdinalIgnoreCase) ||
            name.Equals("Bin", StringComparison.OrdinalIgnoreCase)) return SpecialFolder.Trash;
        if (name.Contains("Locked", StringComparison.OrdinalIgnoreCase)) return SpecialFolder.LockedFolder;
        return SpecialFolder.None;
    }

    /// <summary>True for "Photos from YYYY"-style folders (the main library, not an album).</summary>
    public static bool IsMainLibraryFolder(string folder)
    {
        var name = LastSegment(folder);
        return name.StartsWith("Photos from ", StringComparison.OrdinalIgnoreCase);
    }

    private static string SanitizeAlbum(string folder)
    {
        var name = LastSegment(folder);
        foreach (var c in Path.GetInvalidFileNameChars())
            name = name.Replace(c, '_');
        return string.IsNullOrWhiteSpace(name) ? AllPhotos : name;
    }

    private static string LastSegment(string folder)
    {
        if (string.IsNullOrEmpty(folder)) return string.Empty;
        var trimmed = folder.TrimEnd(PathSeparators);
        var idx = trimmed.LastIndexOfAny(PathSeparators);
        return idx >= 0 ? trimmed[(idx + 1)..] : trimmed;
    }
}
