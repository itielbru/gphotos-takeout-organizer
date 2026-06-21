using System.IO.Compression;
using GPhotosTakeout.Core.IO;
using GPhotosTakeout.Core.Models;

namespace GPhotosTakeout.Core.Archives;

/// <summary>
/// Indexes one or more Takeout ZIP files into a single logical list of entries
/// (without extracting), and extracts individual entries on demand directly to
/// their final destination. Multi-part exports (takeout-001.zip, -002...) are
/// merged so a media/JSON pair split across archives still reconciles.
/// </summary>
public sealed class TakeoutArchiveReader : IDisposable
{
    private readonly record struct Source(string ArchiveId, ZipArchive Archive);

    private readonly List<Source> _sources = new();
    // archiveId|entryFullName -> entry, for on-demand extraction.
    private readonly Dictionary<string, ZipArchiveEntry> _entryLookup = new(StringComparer.Ordinal);
    // ZipArchive is not thread-safe: serialize reads within a single archive,
    // while allowing different archives to be read concurrently.
    private readonly Dictionary<string, SemaphoreSlim> _archiveLocks = new(StringComparer.Ordinal);

    /// <summary>Opens all archives and returns the merged, deduplicated entry index.</summary>
    public IReadOnlyList<TakeoutEntry> Index(IEnumerable<string> zipPaths)
    {
        var entries = new List<TakeoutEntry>();

        foreach (var zipPath in zipPaths)
        {
            var archiveId = Path.GetFileName(zipPath);
            ZipArchive archive;
            try
            {
                archive = ZipFile.OpenRead(LongPath.Extended(zipPath));
            }
            catch (InvalidDataException ex)
            {
                throw new CorruptArchiveException(zipPath, ex);
            }

            _sources.Add(new Source(archiveId, archive));
            _archiveLocks[archiveId] = new SemaphoreSlim(1, 1);

            foreach (var entry in archive.Entries)
            {
                // Skip directory entries (zero-length names ending in '/').
                if (string.IsNullOrEmpty(entry.Name))
                    continue;

                var key = archiveId + "|" + entry.FullName;
                _entryLookup[key] = entry;

                entries.Add(new TakeoutEntry
                {
                    Path = entry.FullName,
                    ArchiveId = archiveId,
                    Length = entry.Length,
                });
            }
        }

        return entries;
    }

    /// <summary>
    /// Extracts one indexed entry to <paramref name="destinationPath"/>, creating
    /// parent directories and tolerating long paths. The content SHA-256 is computed
    /// during the copy so de-duplication needs no extra disk reads.
    /// </summary>
    public async Task<ExtractResult> ExtractAsync(TakeoutEntry entry, string destinationPath, CancellationToken ct = default)
    {
        var (zipEntry, gate) = Resolve(entry);
        await gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            await using var source = zipEntry.Open();
            await using var destination = LongPath.Create(destinationPath);
            using var sha = System.Security.Cryptography.IncrementalHash.CreateHash(
                System.Security.Cryptography.HashAlgorithmName.SHA256);

            var buffer = System.Buffers.ArrayPool<byte>.Shared.Rent(1 << 16);
            try
            {
                long total = 0;
                int read;
                while ((read = await source.ReadAsync(buffer, ct).ConfigureAwait(false)) > 0)
                {
                    sha.AppendData(buffer, 0, read);
                    await destination.WriteAsync(buffer.AsMemory(0, read), ct).ConfigureAwait(false);
                    total += read;
                }
                return new ExtractResult(total, Convert.ToHexString(sha.GetHashAndReset()));
            }
            finally
            {
                System.Buffers.ArrayPool<byte>.Shared.Return(buffer);
            }
        }
        finally
        {
            gate.Release();
        }
    }

    /// <summary>Reads an indexed entry fully into memory (e.g. a small JSON sidecar).</summary>
    public async Task<byte[]> ReadAllBytesAsync(TakeoutEntry entry, CancellationToken ct = default)
    {
        var (zipEntry, gate) = Resolve(entry);
        await gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            await using var source = zipEntry.Open();
            using var buffer = new MemoryStream(capacity: (int)Math.Min(zipEntry.Length, int.MaxValue));
            await source.CopyToAsync(buffer, ct).ConfigureAwait(false);
            return buffer.ToArray();
        }
        finally
        {
            gate.Release();
        }
    }

    private (ZipArchiveEntry entry, SemaphoreSlim gate) Resolve(TakeoutEntry entry)
    {
        var key = entry.ArchiveId + "|" + entry.Path;
        if (!_entryLookup.TryGetValue(key, out var zipEntry))
            throw new FileNotFoundException($"Entry not found in index: {entry.Path}");
        return (zipEntry, _archiveLocks[entry.ArchiveId]);
    }

    public void Dispose()
    {
        foreach (var s in _sources)
            s.Archive.Dispose();
        foreach (var gate in _archiveLocks.Values)
            gate.Dispose();
        _sources.Clear();
        _entryLookup.Clear();
        _archiveLocks.Clear();
    }
}

/// <summary>The outcome of extracting one entry: bytes written and the content SHA-256 (hex).</summary>
public readonly record struct ExtractResult(long Length, string ContentHash);

public sealed class CorruptArchiveException(string archivePath, Exception inner)
    : Exception($"Archive is corrupt or unreadable: {archivePath}", inner)
{
    public string ArchivePath { get; } = archivePath;
}
