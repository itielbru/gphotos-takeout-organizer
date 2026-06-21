namespace GPhotosTakeout.Core.Pipeline;

/// <summary>A progress snapshot streamed to the UI during a run.</summary>
public sealed record ProcessingProgress
{
    public required string Phase { get; init; }
    public int Total { get; init; }
    public int Processed { get; init; }
    public string? CurrentFile { get; init; }
    public int Errors { get; init; }

    public double Fraction => Total > 0 ? (double)Processed / Total : 0;
}

/// <summary>Final tally for a completed (or cancelled) run.</summary>
public sealed record ProcessingReport
{
    public int TotalMedia { get; init; }
    public int Matched { get; init; }
    public int Unmatched { get; init; }
    public int Duplicates { get; init; }
    public int MetadataWritten { get; init; }
    public int SpecialFolderItems { get; init; }
    public int Errors { get; init; }
    public bool Cancelled { get; init; }
    public IReadOnlyList<string> ErrorMessages { get; init; } = Array.Empty<string>();
}
