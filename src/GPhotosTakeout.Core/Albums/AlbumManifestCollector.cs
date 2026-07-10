using System.Collections.Concurrent;
using System.Text.Json;
using GPhotosTakeout.Core.IO;

namespace GPhotosTakeout.Core.Albums;

/// <summary>
/// Collects album membership during a run and writes it as a single
/// <c>albums.json</c> manifest at the output root (the JsonManifest strategy).
/// Thread-safe: Record is called concurrently from the processing loop.
/// </summary>
public sealed class AlbumManifestCollector
{
    public const string FileName = "albums.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private readonly ConcurrentDictionary<string, ConcurrentBag<ManifestFile>> _albums =
        new(StringComparer.OrdinalIgnoreCase);

    public bool HasEntries => !_albums.IsEmpty;

    /// <summary>Records that <paramref name="canonicalPath"/> belongs to <paramref name="albumName"/>.</summary>
    public void Record(string albumName, string fileName, string canonicalPath) =>
        _albums.GetOrAdd(albumName, _ => new ConcurrentBag<ManifestFile>())
            .Add(new ManifestFile(fileName, canonicalPath));

    /// <summary>
    /// Writes the manifest to <c>outputRoot/albums.json</c>. Paths are stored relative
    /// to the output root with forward slashes, so the manifest stays valid if the
    /// library is moved or read on another OS. Entries from an existing manifest are
    /// merged in first: a resumed run skips already-journaled files, so without the
    /// merge their album membership would be lost on rewrite.
    /// </summary>
    public async Task WriteAsync(string outputRoot, CancellationToken ct = default)
    {
        var manifestPath = Path.Combine(outputRoot, FileName);

        // album name -> relative path -> file name (dedupes within and across runs).
        var merged = new SortedDictionary<string, SortedDictionary<string, string>>(StringComparer.OrdinalIgnoreCase);

        MergeExisting(manifestPath, merged);

        foreach (var (album, files) in _albums)
        {
            var bucket = Bucket(merged, album);
            foreach (var f in files)
                bucket[Relative(outputRoot, f.CanonicalPath)] = f.FileName;
        }

        var manifest = new
        {
            schemaVersion = 1,
            generatedAtUtc = DateTime.UtcNow,
            albums = merged
                .Select(a => new
                {
                    name = a.Key,
                    files = a.Value.Select(f => new { fileName = f.Value, path = f.Key }).ToArray(),
                })
                .ToArray(),
        };

        await using var stream = LongPath.Create(manifestPath);
        await JsonSerializer.SerializeAsync(stream, manifest, JsonOptions, ct).ConfigureAwait(false);
    }

    private static void MergeExisting(string manifestPath, SortedDictionary<string, SortedDictionary<string, string>> merged)
    {
        if (!LongPath.Exists(manifestPath))
            return;

        try
        {
            using var stream = LongPath.OpenRead(manifestPath);
            using var doc = JsonDocument.Parse(stream);
            if (!doc.RootElement.TryGetProperty("albums", out var albums) || albums.ValueKind != JsonValueKind.Array)
                return;

            foreach (var album in albums.EnumerateArray())
            {
                if (album.TryGetProperty("name", out var name) && name.GetString() is { } albumName
                    && album.TryGetProperty("files", out var files) && files.ValueKind == JsonValueKind.Array)
                {
                    var bucket = Bucket(merged, albumName);
                    foreach (var f in files.EnumerateArray())
                    {
                        if (f.TryGetProperty("path", out var p) && p.GetString() is { } path)
                            bucket[path] = f.TryGetProperty("fileName", out var fn) ? fn.GetString() ?? "" : "";
                    }
                }
            }
        }
        catch (Exception ex) when (ex is JsonException or IOException)
        {
            // A corrupt or unreadable previous manifest should not fail the run;
            // this run's entries are still written.
        }
    }

    private static SortedDictionary<string, string> Bucket(
        SortedDictionary<string, SortedDictionary<string, string>> merged, string album)
    {
        if (!merged.TryGetValue(album, out var bucket))
            merged[album] = bucket = new SortedDictionary<string, string>(StringComparer.Ordinal);
        return bucket;
    }

    private static string Relative(string outputRoot, string path) =>
        Path.GetRelativePath(outputRoot, path).Replace('\\', '/');

    private readonly record struct ManifestFile(string FileName, string CanonicalPath);
}
