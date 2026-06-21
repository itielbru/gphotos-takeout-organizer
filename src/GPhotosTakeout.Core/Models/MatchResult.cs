namespace GPhotosTakeout.Core.Models;

/// <summary>How a media entry was matched to its JSON sidecar (for logging/diagnostics).</summary>
public enum MatchKind
{
    /// <summary>Exact filename + supplemental-metadata sidecar found.</summary>
    Exact,
    /// <summary>Matched after stripping a truncated supplemental-metadata token.</summary>
    TruncatedSupplemental,
    /// <summary>Matched after normalizing an "-edited" or "(N)" variant.</summary>
    NormalizedVariant,
    /// <summary>No JSON of its own; inherited from a sibling (Live/Motion Photo video).</summary>
    SiblingInherited,
    /// <summary>No sidecar found at all.</summary>
    Unmatched,
}

/// <summary>The outcome of matching one media entry against the global sidecar index.</summary>
public sealed record MatchResult
{
    public required TakeoutEntry Media { get; init; }
    public TakeoutEntry? Sidecar { get; init; }
    public required MatchKind Kind { get; init; }

    public bool IsMatched => Kind != MatchKind.Unmatched;
}
