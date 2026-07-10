namespace GPhotosTakeout.Core.Models;

/// <summary>How the cleaned output tree is organized. Chosen by the user at runtime.</summary>
public enum OutputStructure
{
    /// <summary>ALL_PHOTOS/2023/2023-08/... grouped by capture year and month.</summary>
    YearMonth,
    /// <summary>Preserve the original Google album folders.</summary>
    Albums,
    /// <summary>One flat folder, files sorted by date in their name.</summary>
    Flat,
}

/// <summary>How album membership is materialized alongside ALL_PHOTOS.</summary>
public enum AlbumStrategy
{
    /// <summary>Symlink/hardlink album entries to the single copy in ALL_PHOTOS (default).</summary>
    Shortcut,
    /// <summary>Physically duplicate files into each album folder.</summary>
    Duplicate,
    /// <summary>One folder + an albums.json manifest at the output root describing membership.</summary>
    JsonManifest,
    /// <summary>Ignore album structure entirely.</summary>
    Nothing,
}

/// <summary>What to do when two files have identical content.</summary>
public enum DuplicateHandling
{
    /// <summary>Keep the copy with the shortest name / richest metadata, drop the rest.</summary>
    KeepBest,
    /// <summary>Keep every copy.</summary>
    KeepAll,
}

/// <summary>All user-chosen settings for one processing run.</summary>
public sealed record ProcessingOptions
{
    /// <summary>Default IANA timezone used when a photo has no GPS and the user hasn't overridden it.</summary>
    public const string DefaultFallbackTimeZone = "Asia/Jerusalem";
    public required IReadOnlyList<string> InputZipPaths { get; init; }
    public required string OutputDirectory { get; init; }

    public OutputStructure OutputStructure { get; init; } = OutputStructure.YearMonth;
    public AlbumStrategy AlbumStrategy { get; init; } = AlbumStrategy.Shortcut;
    public DuplicateHandling DuplicateHandling { get; init; } = DuplicateHandling.KeepBest;

    /// <summary>Fallback IANA timezone when a photo has no GPS (e.g. "Asia/Jerusalem").</summary>
    public string? FallbackTimeZone { get; init; }

    /// <summary>Write EXIF/metadata into media (requires bundled ExifTool).</summary>
    public bool WriteMetadata { get; init; } = true;

    /// <summary>
    /// When the sidecar has no usable date, read the capture date embedded in the file
    /// itself (EXIF / QuickTime) after extraction and prefer it over the weaker
    /// filename/folder/modified-time tiers. Managed reader — works without ExifTool.
    /// </summary>
    public bool UseExifFallback { get; init; } = true;

    /// <summary>Parallelism for CPU-bound stages (indexing, hashing, matching).</summary>
    public int CpuParallelism { get; init; } = Math.Max(2, Environment.ProcessorCount - 2);

    /// <summary>Number of concurrent ExifTool processes (I/O-bound — keep small).</summary>
    public int ExifToolParallelism { get; init; } = 4;

    /// <summary>
    /// Max time to wait for ExifTool to finish writing one file before treating the
    /// process as hung (it is then killed and replaced). Large videos can be slow, so
    /// keep this generous.
    /// </summary>
    public TimeSpan ExifToolTimeout { get; init; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Plan only: index, match and resolve dates/destinations and produce a report,
    /// but never extract, de-duplicate, move, or write metadata. Lets the user preview
    /// the resulting organization before committing to a full run.
    /// </summary>
    public bool DryRun { get; init; }
}
