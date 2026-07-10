namespace GPhotosTakeout.Core.Models;

/// <summary>
/// A single logical file discovered while indexing the Takeout archives, before
/// any extraction. Path is the archive-relative path; matching is global and
/// must not depend on which ZIP or folder the entry came from.
/// </summary>
public sealed record TakeoutEntry
{
    /// <summary>Archive-relative path, e.g. "Takeout/Google Photos/Album/IMG.jpg".</summary>
    public required string Path { get; init; }

    /// <summary>File name only, e.g. "IMG.jpg" or "IMG.jpg.supplemental-metadata.json".</summary>
    public string FileName => System.IO.Path.GetFileName(Path);

    /// <summary>Folder portion, used to detect album membership and special folders.</summary>
    public string Folder => System.IO.Path.GetDirectoryName(Path)?.Replace('\\', '/') ?? string.Empty;

    /// <summary>Identifier of the source archive (e.g. "takeout-001.zip"), for resume/logging.</summary>
    public required string ArchiveId { get; init; }

    /// <summary>Uncompressed size in bytes, from the ZIP directory (cheap to read).</summary>
    public long Length { get; init; }

    /// <summary>
    /// Entry last-write time from the ZIP directory. DOS timestamps carry no timezone
    /// and bottom out at 1980; null when absent or invalid. Used only as the
    /// last-resort date fallback tier.
    /// </summary>
    public DateTimeOffset? LastWriteTime { get; init; }

    public bool IsSidecar => Matching.FilenameNormalizer.IsSidecar(FileName);
}
