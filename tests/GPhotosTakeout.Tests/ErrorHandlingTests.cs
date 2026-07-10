using System.IO.Compression;
using System.Text;
using GPhotosTakeout.Core.Archives;
using GPhotosTakeout.Core.Models;
using GPhotosTakeout.Core.Pipeline;
using Xunit;

namespace GPhotosTakeout.Tests;

/// <summary>
/// Tests that the pipeline and its components degrade gracefully under adversarial inputs:
/// corrupt archives, malformed JSON sidecars, missing files, empty archives, etc.
/// </summary>
public class ErrorHandlingTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "gpherr_" + Guid.NewGuid().ToString("N"));

    public ErrorHandlingTests() => Directory.CreateDirectory(_dir);

    // ── Archive errors ──────────────────────────────────────────────────────

    [Fact]
    public void CorruptZip_TruncatedFile_ThrowsCorruptArchiveException()
    {
        var bad = Path.Combine(_dir, "broken.zip");
        File.WriteAllText(bad, "this is not a zip");

        using var reader = new TakeoutArchiveReader();
        var ex = Assert.Throws<CorruptArchiveException>(() => reader.Index(new[] { bad }));
        Assert.Equal(bad, ex.ArchivePath);
    }

    [Fact]
    public void CorruptZip_AllZeros_ThrowsCorruptArchiveException()
    {
        var bad = Path.Combine(_dir, "zeros.zip");
        File.WriteAllBytes(bad, new byte[512]);

        using var reader = new TakeoutArchiveReader();
        Assert.Throws<CorruptArchiveException>(() => reader.Index(new[] { bad }));
    }

    [Fact]
    public void EmptyZip_Indexes_ZeroEntries()
    {
        var empty = Path.Combine(_dir, "empty.zip");
        using (var zip = ZipFile.Open(empty, ZipArchiveMode.Create)) { /* intentionally empty */ }

        using var reader = new TakeoutArchiveReader();
        var entries = reader.Index(new[] { empty });
        Assert.Empty(entries);
    }

    [Fact]
    public async Task EmptyArchive_Pipeline_CompletesWithZeroTotal()
    {
        var empty = Path.Combine(_dir, "empty.zip");
        using (var zip = ZipFile.Open(empty, ZipArchiveMode.Create)) { }

        var output = Path.Combine(_dir, "out");
        var report = await new ProcessingPipeline(null).RunAsync(new ProcessingOptions
        {
            InputZipPaths = new[] { empty },
            OutputDirectory = output,
            WriteMetadata = false,
        });

        Assert.Equal(0, report.TotalMedia);
        Assert.Equal(0, report.Errors);
    }

    // ── JSON sidecar errors ─────────────────────────────────────────────────

    [Fact]
    public async Task MalformedJson_EmptyObject_FileStillPlaced()
    {
        var zip = MakeZip(
            ("Takeout/Google Photos/Photos from 2023/IMG.jpg", new byte[] { 1, 2, 3 }),
            ("Takeout/Google Photos/Photos from 2023/IMG.jpg.supplemental-metadata.json", "{}"u8.ToArray()));

        var output = Path.Combine(_dir, "out-malformed");
        var report = await new ProcessingPipeline(null).RunAsync(new ProcessingOptions
        {
            InputZipPaths = new[] { zip },
            OutputDirectory = output,
            WriteMetadata = false,
            CpuParallelism = 1,
        });

        Assert.Equal(1, report.TotalMedia);
        Assert.Equal(0, report.Errors);
        // No date in JSON, no filename pattern → Undated
        var placed = Directory.EnumerateFiles(output, "IMG.jpg", SearchOption.AllDirectories).FirstOrDefault();
        Assert.NotNull(placed);
    }

    [Fact]
    public async Task MalformedJson_InvalidTimestamp_FileStillPlaced()
    {
        var json = """{"photoTakenTime":{"timestamp":"not-a-number"}}""";
        var zip = MakeZip(
            ("Takeout/Google Photos/Photos from 2023/IMG.jpg", new byte[] { 5 }),
            ("Takeout/Google Photos/Photos from 2023/IMG.jpg.supplemental-metadata.json",
                Encoding.UTF8.GetBytes(json)));

        var output = Path.Combine(_dir, "out-badts");
        var report = await new ProcessingPipeline(null).RunAsync(new ProcessingOptions
        {
            InputZipPaths = new[] { zip },
            OutputDirectory = output,
            WriteMetadata = false,
            CpuParallelism = 1,
        });

        Assert.Equal(1, report.TotalMedia);
        Assert.Equal(0, report.Errors);
    }

    [Fact]
    public async Task MalformedJson_NotJson_FileStillPlaced()
    {
        var zip = MakeZip(
            ("Takeout/Google Photos/Photos from 2023/IMG.jpg", new byte[] { 7 }),
            ("Takeout/Google Photos/Photos from 2023/IMG.jpg.supplemental-metadata.json",
                Encoding.UTF8.GetBytes("this is not json at all <<<>>>")));

        var output = Path.Combine(_dir, "out-notjson");
        var report = await new ProcessingPipeline(null).RunAsync(new ProcessingOptions
        {
            InputZipPaths = new[] { zip },
            OutputDirectory = output,
            WriteMetadata = false,
            CpuParallelism = 1,
        });

        // Sidecar is unreadable but the photo must still be placed.
        Assert.Equal(1, report.TotalMedia);
        Assert.Equal(0, report.Errors);
    }

    [Fact]
    public async Task MalformedJson_UnicodeTitle_DoesNotCrash()
    {
        var json = """{"title":"שלום","photoTakenTime":{"timestamp":"1692108336"}}""";
        var zip = MakeZip(
            ("Takeout/Google Photos/Photos from 2023/IMG.jpg", new byte[] { 8 }),
            ("Takeout/Google Photos/Photos from 2023/IMG.jpg.supplemental-metadata.json",
                Encoding.UTF8.GetBytes(json)));

        var output = Path.Combine(_dir, "out-unicode");
        var report = await new ProcessingPipeline(null).RunAsync(new ProcessingOptions
        {
            InputZipPaths = new[] { zip },
            OutputDirectory = output,
            WriteMetadata = false,
            CpuParallelism = 1,
        });

        Assert.Equal(1, report.TotalMedia);
        Assert.Equal(0, report.Errors);
    }

    // ── ExifTool missing at runtime ─────────────────────────────────────────

    [Fact]
    public async Task ExifToolNotFound_PipelineFallsBackWithoutMetadata()
    {
        var zip = MakeZip(
            ("Takeout/Google Photos/Photos from 2023/IMG.jpg",
                new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 })); // JPEG magic bytes

        var output = Path.Combine(_dir, "out-noexif");
        // Pass a non-existent exiftool path: the pipeline should gracefully degrade.
        var fakeTool = Path.Combine(_dir, "nonexistent-exiftool.exe");
        var report = await new ProcessingPipeline(fakeTool).RunAsync(new ProcessingOptions
        {
            InputZipPaths = new[] { zip },
            OutputDirectory = output,
            WriteMetadata = true,
            CpuParallelism = 1,
        });

        // File should still be placed; errors may report ExifTool start failure.
        Assert.Equal(1, report.TotalMedia);
        var placed = Directory.EnumerateFiles(output, "*.jpg", SearchOption.AllDirectories).FirstOrDefault();
        Assert.NotNull(placed);
    }

    // ── Dedup salvage path ─────────────────────────────────────────────────

    [Fact]
    public async Task DuplicateHandling_KeepAll_PreservesAllCopies()
    {
        const int copies = 3;
        var content = new byte[1024];
        new Random(42).NextBytes(content);

        var zip = MakeZip(
            Enumerable.Range(0, copies)
                .SelectMany(i => new[]
                {
                    ($"Takeout/Google Photos/Album{i}/IMG.jpg", content),
                }).ToArray());

        var output = Path.Combine(_dir, "out-keepall");
        var report = await new ProcessingPipeline(null).RunAsync(new ProcessingOptions
        {
            InputZipPaths = new[] { zip },
            OutputDirectory = output,
            OutputStructure = OutputStructure.YearMonth,
            DuplicateHandling = DuplicateHandling.KeepAll,
            WriteMetadata = false,
            CpuParallelism = 1,
        });

        Assert.Equal(copies, report.TotalMedia);
        // With KeepAll, duplicates counter stays at zero.
        Assert.Equal(0, report.Duplicates);
        Assert.Equal(0, report.Errors);
        // All copies placed (unique names) in the main library. Album membership is
        // additionally materialized under Albums/ (default Shortcut strategy), so the
        // count is scoped to ALL_PHOTOS.
        var placed = Directory.EnumerateFiles(Path.Combine(output, "ALL_PHOTOS"), "IMG*.jpg",
            SearchOption.AllDirectories).Count();
        Assert.Equal(copies, placed);
        Assert.True(Directory.Exists(Path.Combine(output, "Albums")),
            "album membership should be materialized under Albums/ even with KeepAll");
    }

    // ── Helpers ────────────────────────────────────────────────────────────

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
