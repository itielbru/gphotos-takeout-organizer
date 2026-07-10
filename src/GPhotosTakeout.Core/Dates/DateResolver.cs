using System.Globalization;
using System.Text.RegularExpressions;
using GPhotosTakeout.Core.Models;

namespace GPhotosTakeout.Core.Dates;

public enum DateSource { Json, Exif, Filename, AlbumFolder, FileModified, None }

/// <summary>
/// A resolved capture time. <see cref="IsUtc"/> distinguishes a true UTC instant
/// (from JSON, which should be converted to local via the GPS timezone) from a
/// naive local wall-clock time (from a filename, which is already local).
/// </summary>
public readonly record struct ResolvedDate(DateTime Value, DateSource Source, bool IsUtc)
{
    public static readonly ResolvedDate None = new(default, DateSource.None, false);
    public bool HasValue => Source != DateSource.None;
}

/// <summary>
/// Determines a media file's capture time using a priority hierarchy:
/// JSON photoTakenTime → existing EXIF → filename pattern → album-folder year →
/// filesystem modified time. Pure and unit-testable; callers supply the inputs.
/// </summary>
public sealed class DateResolver
{
    // IMG_20230815_142536 / PXL_20230815_142536123 / VID_20230815_142536
    private static readonly Regex CompactDateTime =
        new(@"(?<!\d)(20\d{2})(\d{2})(\d{2})[_\-](\d{2})(\d{2})(\d{2})", RegexOptions.Compiled);

    // Screenshot_2023-08-15-14-25-36
    private static readonly Regex DashedDateTime =
        new(@"(?<!\d)(20\d{2})-(\d{2})-(\d{2})[-_ ](\d{2})[-:](\d{2})[-:](\d{2})", RegexOptions.Compiled);

    // WhatsApp IMG-20230815-WA0001 (date only)
    private static readonly Regex CompactDateOnly =
        new(@"(?<!\d)(20\d{2})(\d{2})(\d{2})(?!\d)", RegexOptions.Compiled);

    // "Photos from 2023" album folder
    private static readonly Regex FolderYear =
        new(@"(?:^|[ /\\])(20\d{2})(?:[ /\\]|$)", RegexOptions.Compiled);

    public ResolvedDate Resolve(
        string fileName,
        TakeoutJson? json,
        ExifDate? exif,
        string? folder,
        DateTime? fileModifiedUtc)
    {
        if (json?.CapturedUtc is { } captured)
            return new ResolvedDate(captured.UtcDateTime, DateSource.Json, IsUtc: true);

        if (exif is { } e)
            return new ResolvedDate(e.Value, DateSource.Exif, e.IsUtc);

        if (TryFromFilename(fileName, out var fromName))
            return new ResolvedDate(fromName, DateSource.Filename, IsUtc: false);

        if (folder is not null && TryYearFromFolder(folder, out var fromFolder))
            return new ResolvedDate(fromFolder, DateSource.AlbumFolder, IsUtc: false);

        if (fileModifiedUtc is { } mtime)
            return new ResolvedDate(mtime, DateSource.FileModified, IsUtc: true);

        return ResolvedDate.None;
    }

    public static bool TryFromFilename(string fileName, out DateTime value)
    {
        Match m;
        if ((m = CompactDateTime.Match(fileName)).Success || (m = DashedDateTime.Match(fileName)).Success)
        {
            if (TryBuild(m, hasTime: true, out value))
                return true;
        }

        if ((m = CompactDateOnly.Match(fileName)).Success && TryBuild(m, hasTime: false, out value))
            return true;

        value = default;
        return false;
    }

    private static bool TryYearFromFolder(string folder, out DateTime value)
    {
        var m = FolderYear.Match(folder);
        if (m.Success && int.TryParse(m.Groups[1].Value, out var year) && year is >= 1990 and <= 2100)
        {
            value = new DateTime(year, 1, 1, 0, 0, 0, DateTimeKind.Unspecified);
            return true;
        }
        value = default;
        return false;
    }

    private static bool TryBuild(Match m, bool hasTime, out DateTime value)
    {
        var year = int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
        var month = int.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture);
        var day = int.Parse(m.Groups[3].Value, CultureInfo.InvariantCulture);
        var hour = hasTime ? int.Parse(m.Groups[4].Value, CultureInfo.InvariantCulture) : 0;
        var min = hasTime ? int.Parse(m.Groups[5].Value, CultureInfo.InvariantCulture) : 0;
        var sec = hasTime ? int.Parse(m.Groups[6].Value, CultureInfo.InvariantCulture) : 0;

        if (month is < 1 or > 12 || day is < 1 or > 31 || hour > 23 || min > 59 || sec > 59)
        {
            value = default;
            return false;
        }

        try
        {
            value = new DateTime(year, month, day, hour, min, sec, DateTimeKind.Unspecified);
            return true;
        }
        catch (ArgumentOutOfRangeException)
        {
            value = default;
            return false;
        }
    }
}
