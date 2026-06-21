namespace GPhotosTakeout.Core.Pipeline;

/// <summary>A progress snapshot streamed to the UI during a run.</summary>
public sealed record ProcessingProgress
{
    public required string Phase { get; init; }
    public int Total { get; init; }
    public int Processed { get; init; }
    public string? CurrentFile { get; init; }
    public int Errors { get; init; }

    /// <summary>Seconds elapsed since the run started.</summary>
    public double ElapsedSeconds { get; init; }

    public double Fraction => Total > 0 ? (double)Processed / Total : 0;

    /// <summary>Files processed per second so far (0 until the first item completes).</summary>
    public double ItemsPerSecond => ElapsedSeconds > 0 ? Processed / ElapsedSeconds : 0;

    /// <summary>Estimated seconds remaining, or null when it cannot be computed yet.</summary>
    public double? EtaSeconds =>
        ItemsPerSecond > 0 && Total > Processed ? (Total - Processed) / ItemsPerSecond : null;
}

/// <summary>What happened to a single media file during the run.</summary>
public sealed record FileOutcome
{
    public required string FileName { get; init; }
    public required string SourceFolder { get; init; }
    public bool Matched { get; init; }
    public string? DateSource { get; init; }
    public string? DestinationPath { get; init; }
    public bool MetadataWritten { get; init; }
    public bool IsDuplicate { get; init; }
    public bool Planned { get; init; }       // dry-run: computed but not executed
    public string? Error { get; init; }
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
    public bool DryRun { get; init; }
    public IReadOnlyList<string> ErrorMessages { get; init; } = Array.Empty<string>();
    public IReadOnlyList<FileOutcome> Outcomes { get; init; } = Array.Empty<FileOutcome>();
}
