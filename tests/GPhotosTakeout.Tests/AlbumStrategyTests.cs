using System.IO.Compression;
using GPhotosTakeout.Core.Models;
using GPhotosTakeout.Core.Pipeline;
using Xunit;

namespace GPhotosTakeout.Tests;

/// <summary>
/// Integration tests for album handling: album-named output folders, the Nothing strategy
/// (no albums written), and the Shortcut strategy (symlink → hardlink → copy fallback).
/// </summary>
public class AlbumStrategyTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "gphalbumstrat_" + Guid.NewGuid().ToString("N"));

    public AlbumStrategyTests() => Directory.CreateDirectory(_dir);

    // ── OutputStructure.Albums ─────────────────────────────────────────────────

    [Fact]
    public async Task AlbumsStructure_PlacesFilesInAlbumFolders()
    {
        var zip = MakeZip(
            ("Takeout/Google Photos/Summer Trip/IMG.jpg", new byte[] { 1, 2, 3 }));

        var output = Path.Combine(_dir, "out-albums");
        var report = await new ProcessingPipeline(null).RunAsync(new ProcessingOptions
        {
            InputZipPaths = new[] { zip },
            OutputDirectory = output,
            OutputStructure = OutputStructure.Albums,
            WriteMetadata = false,
            CpuParallelism = 1,
        });

        Assert.Equal(1, report.TotalMedia);
        Assert.Equal(0, report.Errors);

        // File should land in the album-named subfolder under ALL_PHOTOS.
        var placed = Directory.EnumerateFiles(output, "IMG.jpg", SearchOption.AllDirectories).FirstOrDefault();
        Assert.NotNull(placed);
        Assert.Contains("Summer Trip", placed, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AlbumsStructure_MultipleAlbums_EachInOwnFolder()
    {
        var zip = MakeZip(
            ("Takeout/Google Photos/Beach/A.jpg", new byte[] { 1 }),
            ("Takeout/Google Photos/Mountains/B.jpg", new byte[] { 2 }));

        var output = Path.Combine(_dir, "out-multialb");
        var report = await new ProcessingPipeline(null).RunAsync(new ProcessingOptions
        {
            InputZipPaths = new[] { zip },
            OutputDirectory = output,
            OutputStructure = OutputStructure.Albums,
            WriteMetadata = false,
            CpuParallelism = 1,
        });

        Assert.Equal(2, report.TotalMedia);
        Assert.Equal(0, report.Errors);

        var beachFile = Directory.EnumerateFiles(output, "A.jpg", SearchOption.AllDirectories).FirstOrDefault();
        var mountainFile = Directory.EnumerateFiles(output, "B.jpg", SearchOption.AllDirectories).FirstOrDefault();
        Assert.NotNull(beachFile);
        Assert.NotNull(mountainFile);
        Assert.Contains("Beach", beachFile, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Mountains", mountainFile, StringComparison.OrdinalIgnoreCase);
    }

    // ── OutputStructure.Flat ─────────────────────────────────────────────────

    [Fact]
    public async Task FlatStructure_AllFilesInAllPhotos()
    {
        var zip = MakeZip(
            ("Takeout/Google Photos/Photos from 2023/A.jpg", new byte[] { 1 }),
            ("Takeout/Google Photos/Trip/B.jpg", new byte[] { 2 }));

        var output = Path.Combine(_dir, "out-flat");
        var report = await new ProcessingPipeline(null).RunAsync(new ProcessingOptions
        {
            InputZipPaths = new[] { zip },
            OutputDirectory = output,
            OutputStructure = OutputStructure.Flat,
            WriteMetadata = false,
            CpuParallelism = 1,
        });

        Assert.Equal(2, report.TotalMedia);
        Assert.Equal(0, report.Errors);

        // Both files should land directly in ALL_PHOTOS (no subdirectory nesting).
        var allPhotos = Path.Combine(output, "ALL_PHOTOS");
        Assert.True(Directory.Exists(allPhotos));
        var files = Directory.GetFiles(allPhotos, "*", SearchOption.TopDirectoryOnly);
        Assert.Equal(2, files.Length);
    }

    // ── AlbumStrategy.Nothing ─────────────────────────────────────────────────

    [Fact]
    public async Task AlbumStrategyNothing_NoAlbumsFolder()
    {
        var zip = MakeZip(
            ("Takeout/Google Photos/Photos from 2023/A.jpg", new byte[] { 1 }),
            ("Takeout/Google Photos/VacationAlbum/A.jpg", new byte[] { 1 }));

        var output = Path.Combine(_dir, "out-nothing");
        var report = await new ProcessingPipeline(null).RunAsync(new ProcessingOptions
        {
            InputZipPaths = new[] { zip },
            OutputDirectory = output,
            OutputStructure = OutputStructure.YearMonth,
            AlbumStrategy = AlbumStrategy.Nothing,
            WriteMetadata = false,
            CpuParallelism = 1,
        });

        Assert.Equal(0, report.Errors);

        // Albums folder must not exist, and no manifest either.
        var albumsDir = Path.Combine(output, "Albums");
        Assert.False(Directory.Exists(albumsDir));
        Assert.False(File.Exists(Path.Combine(output, Core.Albums.AlbumManifestCollector.FileName)));
    }

    // ── AlbumStrategy.Shortcut ────────────────────────────────────────────────

    [Fact]
    public async Task AlbumStrategyShortcut_AlbumCopyLinkedToCanonical()
    {
        // Identical content: canonical in "Photos from 2023", album copy in "VacationAlbum".
        var content = new byte[] { 0x10, 0x20, 0x30, 0x40 };
        var zip = MakeZip(
            ("Takeout/Google Photos/Photos from 2023/IMG.jpg", content),
            ("Takeout/Google Photos/VacationAlbum/IMG.jpg", content));

        var output = Path.Combine(_dir, "out-shortcut");
        var report = await new ProcessingPipeline(null).RunAsync(new ProcessingOptions
        {
            InputZipPaths = new[] { zip },
            OutputDirectory = output,
            OutputStructure = OutputStructure.YearMonth,
            AlbumStrategy = AlbumStrategy.Shortcut,
            DuplicateHandling = DuplicateHandling.KeepBest,
            WriteMetadata = false,
            CpuParallelism = 1,
        });

        Assert.Equal(0, report.Errors);

        // The canonical copy must exist somewhere under ALL_PHOTOS.
        var canonical = Directory.EnumerateFiles(output, "IMG.jpg", SearchOption.AllDirectories)
            .FirstOrDefault(p => p.Contains("ALL_PHOTOS", StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(canonical);

        // The album link (symlink / hardlink / copy fallback) must also exist,
        // regardless of which copy won the dedup race.
        var albumEntry = Path.Combine(output, "Albums", "VacationAlbum", "IMG.jpg");
        Assert.True(File.Exists(albumEntry), "album entry must exist");
    }

    [Fact]
    public async Task AlbumStrategyShortcut_AlbumCopyFirstInArchive_StillCreatesAlbumEntry()
    {
        // Regression: when the album copy is extracted first it wins the dedup race
        // and becomes the canonical file. The album entry used to be created only on
        // the losing (duplicate) path, so this ordering produced no Albums/ entry.
        var content = new byte[] { 0x10, 0x20, 0x30, 0x40 };
        var zip = MakeZip(
            ("Takeout/Google Photos/VacationAlbum/IMG.jpg", content),
            ("Takeout/Google Photos/Photos from 2023/IMG.jpg", content));

        var output = Path.Combine(_dir, "out-shortcut-race");
        var report = await new ProcessingPipeline(null).RunAsync(new ProcessingOptions
        {
            InputZipPaths = new[] { zip },
            OutputDirectory = output,
            OutputStructure = OutputStructure.YearMonth,
            AlbumStrategy = AlbumStrategy.Shortcut,
            DuplicateHandling = DuplicateHandling.KeepBest,
            WriteMetadata = false,
            CpuParallelism = 1, // deterministic: archive order decides the race
        });

        Assert.Equal(0, report.Errors);
        Assert.True(File.Exists(Path.Combine(output, "Albums", "VacationAlbum", "IMG.jpg")),
            "album entry must exist even when the album copy wins the dedup race");
    }

    // ── AlbumStrategy.Duplicate ───────────────────────────────────────────────

    [Fact]
    public async Task AlbumStrategyDuplicate_PlacesPhysicalCopyInAlbumFolder()
    {
        var content = new byte[] { 1, 2, 3, 4, 5 };
        var zip = MakeZip(
            ("Takeout/Google Photos/Photos from 2023/IMG.jpg", content),
            ("Takeout/Google Photos/VacationAlbum/IMG.jpg", content));

        var output = Path.Combine(_dir, "out-duplicate");
        var report = await new ProcessingPipeline(null).RunAsync(new ProcessingOptions
        {
            InputZipPaths = new[] { zip },
            OutputDirectory = output,
            OutputStructure = OutputStructure.YearMonth,
            AlbumStrategy = AlbumStrategy.Duplicate,
            DuplicateHandling = DuplicateHandling.KeepBest,
            WriteMetadata = false,
            CpuParallelism = 1,
        });

        Assert.Equal(0, report.Errors);

        var albumEntry = Path.Combine(output, "Albums", "VacationAlbum", "IMG.jpg");
        Assert.True(File.Exists(albumEntry), "album folder must contain the file");
        // A physical copy, not a symlink.
        Assert.Null(File.ResolveLinkTarget(albumEntry, returnFinalTarget: false));
        Assert.Equal(content, File.ReadAllBytes(albumEntry));
    }

    // ── AlbumStrategy.JsonManifest ────────────────────────────────────────────

    [Fact]
    public async Task AlbumStrategyJsonManifest_WritesManifestWithRelativePaths()
    {
        var content = new byte[] { 7, 7, 7 };
        var zip = MakeZip(
            ("Takeout/Google Photos/Photos from 2023/IMG_20230815_120000.jpg", content),
            ("Takeout/Google Photos/VacationAlbum/IMG_20230815_120000.jpg", content));

        var output = Path.Combine(_dir, "out-manifest");
        var report = await new ProcessingPipeline(null).RunAsync(new ProcessingOptions
        {
            InputZipPaths = new[] { zip },
            OutputDirectory = output,
            OutputStructure = OutputStructure.YearMonth,
            AlbumStrategy = AlbumStrategy.JsonManifest,
            DuplicateHandling = DuplicateHandling.KeepBest,
            WriteMetadata = false,
            CpuParallelism = 1,
        });

        Assert.Equal(0, report.Errors);
        // Manifest instead of an Albums folder.
        Assert.False(Directory.Exists(Path.Combine(output, "Albums")));

        var manifestPath = Path.Combine(output, Core.Albums.AlbumManifestCollector.FileName);
        Assert.True(File.Exists(manifestPath), "albums.json must be written at the output root");

        using var doc = System.Text.Json.JsonDocument.Parse(File.ReadAllText(manifestPath));
        Assert.Equal(1, doc.RootElement.GetProperty("schemaVersion").GetInt32());
        var album = Assert.Single(doc.RootElement.GetProperty("albums").EnumerateArray());
        Assert.Equal("VacationAlbum", album.GetProperty("name").GetString());
        var file = Assert.Single(album.GetProperty("files").EnumerateArray());
        Assert.Equal("IMG_20230815_120000.jpg", file.GetProperty("fileName").GetString());
        var relPath = file.GetProperty("path").GetString();
        Assert.NotNull(relPath);
        Assert.DoesNotContain('\\', relPath);          // forward slashes only
        Assert.False(Path.IsPathRooted(relPath));      // relative to the output root
        Assert.True(File.Exists(Path.Combine(output, relPath)),
            "manifest path must resolve to the canonical file");
    }

    [Fact]
    public async Task AlbumStrategyJsonManifest_SecondRunMergesExistingManifest()
    {
        var contentA = new byte[] { 1 };
        var contentB = new byte[] { 2 };
        var zipA = MakeZip(("Takeout/Google Photos/AlbumA/A.jpg", contentA));
        var zipB = MakeZip(("Takeout/Google Photos/AlbumB/B.jpg", contentB));

        var output = Path.Combine(_dir, "out-manifest-merge");
        ProcessingOptions Options(string zip) => new()
        {
            InputZipPaths = new[] { zip },
            OutputDirectory = output,
            OutputStructure = OutputStructure.YearMonth,
            AlbumStrategy = AlbumStrategy.JsonManifest,
            WriteMetadata = false,
            CpuParallelism = 1,
        };

        await new ProcessingPipeline(null).RunAsync(Options(zipA));
        await new ProcessingPipeline(null).RunAsync(Options(zipB));

        using var doc = System.Text.Json.JsonDocument.Parse(
            File.ReadAllText(Path.Combine(output, Core.Albums.AlbumManifestCollector.FileName)));
        var names = doc.RootElement.GetProperty("albums").EnumerateArray()
            .Select(a => a.GetProperty("name").GetString()).ToArray();
        Assert.Equal(new[] { "AlbumA", "AlbumB" }, names);
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    private string MakeZip(params (string path, byte[] bytes)[] entries)
    {
        var zipPath = Path.Combine(_dir, $"take-{Guid.NewGuid():N}.zip");
        using var zip = ZipFile.Open(zipPath, ZipArchiveMode.Create);
        foreach (var (path, bytes) in entries)
        {
            var e = zip.CreateEntry(path);
            using var s = e.Open();
            s.Write(bytes);
        }
        return zipPath;
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { /* ignore */ }
    }
}
