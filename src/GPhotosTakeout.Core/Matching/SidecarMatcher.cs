using GPhotosTakeout.Core.Models;

namespace GPhotosTakeout.Core.Matching;

/// <summary>
/// Matches media files to their Google Photos JSON sidecars using a single global
/// index built across all archives and folders (cross-folder matching — a media
/// file in "Album X" may have its sidecar in "Photos from 2023").
///
/// Strategy (staged fallback, per the design doc):
///   1. Index every sidecar by the normalized key of its recovered media name.
///   2. For each media file, normalize to the same key and look it up.
///   3. Media with no sidecar of its own (Motion/Live Photo videos) inherit from
///      a sibling media file that shares the same key, if one is matched.
/// </summary>
public sealed class SidecarMatcher
{
    // Extensions whose video half typically ships without its own JSON and should
    // inherit metadata from a sibling still image (Live/Motion Photos).
    private static readonly HashSet<string> SiblinglessVideoExtensions =
        new(StringComparer.OrdinalIgnoreCase) { ".mp", ".mv", ".mov", ".mp4" };

    /// <summary>
    /// Matches all media entries in the index. Sidecars themselves are excluded
    /// from the output; every non-sidecar entry yields exactly one MatchResult.
    /// </summary>
    public IReadOnlyList<MatchResult> Match(IEnumerable<TakeoutEntry> entries)
    {
        var all = entries.ToList();

        // Index sidecars by normalized media key. When several sidecars collapse
        // to the same key (e.g. duplicates across split archives), keep the first;
        // dedup happens later in the pipeline on file content.
        var sidecarByKey = new Dictionary<string, TakeoutEntry>(StringComparer.Ordinal);
        foreach (var e in all.Where(e => e.IsSidecar))
        {
            var recovered = FilenameNormalizer.RecoverMediaNameFromSidecar(e.FileName);
            var key = FilenameNormalizer.NormalizeToKey(recovered);
            sidecarByKey.TryAdd(key, e);
        }

        var media = all.Where(e => !e.IsSidecar).ToList();
        var results = new List<MatchResult>(media.Count);

        // Single pass: every media file is matched by its normalized key. Because
        // normalization collapses same-stem media (IMG.jpg, IMG.MP, IMG-edited.jpg)
        // to one key, a Motion/Live Photo video and its still share a key and the
        // video resolves to the same sidecar without a separate sibling pass.
        foreach (var m in media)
        {
            var key = FilenameNormalizer.NormalizeToKey(m.FileName);
            var sidecar = sidecarByKey.GetValueOrDefault(key);
            results.Add(new MatchResult
            {
                Media = m,
                Sidecar = sidecar,
                Kind = sidecar is null ? MatchKind.Unmatched : ClassifyMatch(m, sidecar),
            });
        }

        return results;
    }

    private static MatchKind ClassifyMatch(TakeoutEntry media, TakeoutEntry sidecar)
    {
        var recovered = FilenameNormalizer.RecoverMediaNameFromSidecar(sidecar.FileName);
        if (string.Equals(recovered, media.FileName, StringComparison.OrdinalIgnoreCase))
            return MatchKind.Exact;

        // A video that resolved to a sidecar whose recovered name is a *different*
        // (still-image) file is the Motion/Live Photo case: inherited from a sibling.
        var mediaExt = Path.GetExtension(media.FileName);
        var sidecarMediaExt = Path.GetExtension(recovered);
        if (SiblinglessVideoExtensions.Contains(mediaExt) &&
            !mediaExt.Equals(sidecarMediaExt, StringComparison.OrdinalIgnoreCase))
            return MatchKind.SiblingInherited;

        var hadFullToken = sidecar.FileName.Contains(".supplemental-metadata.", StringComparison.OrdinalIgnoreCase);
        return hadFullToken ? MatchKind.TruncatedSupplemental : MatchKind.NormalizedVariant;
    }
}
