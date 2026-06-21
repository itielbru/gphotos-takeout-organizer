using System.Text.RegularExpressions;

namespace GPhotosTakeout.Core.Matching;

/// <summary>
/// Handles the messy Google Photos Takeout filename conventions so that a media
/// file can be matched to its JSON sidecar. This is the core of "Issue #353".
///
/// Google's JSON sidecar naming has several forms:
///   old format:   IMG_1234.jpg.json
///   new (2024+):  IMG_1234.jpg.supplemental-metadata.json
///   truncated:    IMG_1234.jpg.supplemental-metad.json
///                 IMG_1234.jpg.supplem.json   ...etc (cut at any point)
///
/// The whole filename is also capped in length, and edited/duplicate variants
/// add suffixes like "-edited" or "(1)" that do not line up cleanly. We never
/// hardcode the truncation length (sources disagree: 46 vs 47). Instead we strip
/// any trailing ".&lt;prefix-of("supplemental-metadata")&gt;" token.
/// </summary>
public static class FilenameNormalizer
{
    private const string SupplementalWord = "supplemental-metadata";

    // "-edited" markers Google appends to edited copies, across export locales.
    // The key (English) form covers most exports; localized forms are common too.
    private static readonly string[] EditedMarkers =
    {
        "-edited", "-bearbeitet", "-modifié", "-ha editado", "-editado",
        "-bewerkt", "-redigerad", "-muokattu", "-edytowano", "-编辑",
    };

    // Trailing duplicate counter such as "(1)" or "(12)".
    private static readonly Regex DuplicateCounter =
        new(@"\((\d{1,3})\)$", RegexOptions.Compiled);

    /// <summary>
    /// True if the given filename looks like a Google JSON sidecar (.json,
    /// optionally with a supplemental-metadata token, possibly truncated).
    /// </summary>
    public static bool IsSidecar(string fileName) =>
        fileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Given a JSON sidecar filename, recover the media filename it describes by
    /// removing the trailing ".json" and any (possibly truncated)
    /// ".supplemental-metadata" token. The result may or may not still include
    /// the media extension, depending on how Google named it.
    /// </summary>
    /// <example>
    /// "IMG_1234.jpg.supplemental-metadata.json" -> "IMG_1234.jpg"
    /// "IMG_1234.jpg.supplem.json"               -> "IMG_1234.jpg"
    /// "IMG_1234.jpg.json"                       -> "IMG_1234.jpg"
    /// "IMG_1234.json"                           -> "IMG_1234"
    /// </example>
    public static string RecoverMediaNameFromSidecar(string sidecarFileName)
    {
        if (!IsSidecar(sidecarFileName))
            return sidecarFileName;

        var name = sidecarFileName[..^".json".Length];
        return StripSupplementalToken(name);
    }

    /// <summary>
    /// Removes a trailing ".&lt;token&gt;" where token is any non-empty prefix of
    /// "supplemental-metadata". Returns the input unchanged when no such token is
    /// present (e.g. the old "IMG.jpg.json" format).
    /// </summary>
    private static string StripSupplementalToken(string name)
    {
        var lastDot = name.LastIndexOf('.');
        if (lastDot < 0)
            return name;

        var token = name[(lastDot + 1)..];
        if (token.Length > 0 && IsPrefixOfSupplemental(token))
            return name[..lastDot];

        return name;
    }

    private static bool IsPrefixOfSupplemental(string token)
    {
        // Avoid eating a real extension like ".jpg": only treat the token as the
        // supplemental marker if it is genuinely a prefix of the magic word and
        // long enough to be unambiguous (the word starts with "suppl...").
        if (token.Length > SupplementalWord.Length)
            return false;
        return SupplementalWord.StartsWith(token, StringComparison.OrdinalIgnoreCase)
               && token.StartsWith("s", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Reduces a media filename to a normalized matching key: lowercase, no
    /// extension, no "-edited" marker, and no "(N)" duplicate counter. Both a
    /// media file and the recovered name of its sidecar should map to the same
    /// key.
    /// </summary>
    /// <example>
    /// "IMG_1234.jpg"          -> "img_1234"
    /// "IMG_1234-edited.jpg"   -> "img_1234"
    /// "IMG_1234(1).jpg"       -> "img_1234"
    /// "IMG_1234.jpg"  (from sidecar recovery) and the file itself collide.
    /// </example>
    public static string NormalizeToKey(string mediaFileName)
    {
        var name = StripKnownMediaExtension(mediaFileName);

        // "-edited" and "(N)" can appear in either order ("IMG-edited(1)" or
        // "IMG(1)-edited"), so strip both repeatedly until the name stops changing.
        string previous;
        do
        {
            previous = name;
            name = RemoveEditedMarker(name);
            name = RemoveDuplicateCounter(name);
        } while (name != previous);

        return name.Trim().ToLowerInvariant();
    }

    private static string StripKnownMediaExtension(string name)
    {
        var ext = Path.GetExtension(name);
        // Only strip a plausible media extension (1-5 chars). The recovered
        // sidecar name may have no extension at all, which is fine.
        if (ext.Length is >= 2 and <= 6)
            return name[..^ext.Length];
        return name;
    }

    private static string RemoveEditedMarker(string name)
    {
        foreach (var marker in EditedMarkers)
        {
            if (name.EndsWith(marker, StringComparison.OrdinalIgnoreCase))
                return name[..^marker.Length];
        }
        return name;
    }

    private static string RemoveDuplicateCounter(string name)
    {
        var m = DuplicateCounter.Match(name);
        return m.Success ? name[..m.Index] : name;
    }
}
