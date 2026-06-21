using System.IO.Compression;
using System.Text;
using GPhotosTakeout.Core.Dedup;
using GPhotosTakeout.Core.Models;
using GPhotosTakeout.Core.Pipeline;
using Xunit;

namespace GPhotosTakeout.Tests;

/// <summary>
/// Regression tests for the parallel-execution bugs: a destination-name race that
/// dropped media files, dedup TOCTOU, and cancellation that lost the report. These
/// exercise the real default code path (parallelism &gt; 1), unlike the single-
/// threaded integration tests.
/// </summary>
public class ConcurrencyTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "gphconc_" + Guid.NewGuid().ToString("N"));

    public ConcurrencyTests() => Directory.CreateDirectory(_dir);

    private static string Sidecar(long epoch) =>
        "{\"photoTakenTime\":{\"timestamp\":\"" + epoch + "\"}}";

    [Fact]
    public void Dedup_ConcurrentIdenticalContent_ExactlyOneOwner()
    {
        // Many threads register byte-identical content at once: exactly one must be
        // told "you are new" (null) and every other must resolve to that one path.
        var content = new byte[64 * 1024];
        new Random(11).NextBytes(content);
        var paths = Enumerable.Range(0, 32).Select(i =>
        {
            var p = Path.Combine(_dir, $"copy_{i}.bin");
            File.WriteAllBytes(p, content);
            return p;
        }).ToArray();

        var dedup = new HashDeduplicator();
        var results = new string?[paths.Length];
        Parallel.For(0, paths.Length, i => results[i] = dedup.FindDuplicateOrRegister(paths[i]));

        var owners = results.Count(r => r is null);
        Assert.Equal(1, owners);
        var ownerPath = paths[Array.IndexOf(results, null)];
        Assert.All(results.Where(r => r is not null), r => Assert.Equal(ownerPath, r));
    }

    [Fact]
    public async Task Pipeline_HighParallelism_DistinctFilesSameDest_NoneLost()
    {
        // 40 *different* photos that all share the filename IMG_1234.jpg and the same
        // capture month, so every one maps to the same destination path. Under the
        // old MakeUnique+Move race a colliding move threw and silently dropped a file.
        const int n = 40;
        const long epoch = 1692108336; // 2023-08-15 UTC
        var zipPath = Path.Combine(_dir, "takeout-001.zip");
        using (var zip = ZipFile.Open(zipPath, ZipArchiveMode.Create))
        {
            for (var i = 0; i < n; i++)
            {
                var bytes = new byte[2048];
                new Random(1000 + i).NextBytes(bytes); // distinct content => not duplicates
                AddBinary(zip, $"Takeout/Google Photos/Album{i:D2}/IMG_1234.jpg", bytes);
                AddText(zip, $"Takeout/Google Photos/Album{i:D2}/IMG_1234.jpg.supplemental-metadata.json", Sidecar(epoch));
            }
        }

        var output = Path.Combine(_dir, "out");
        var report = await new ProcessingPipeline(null).RunAsync(new ProcessingOptions
        {
            InputZipPaths = new[] { zipPath },
            OutputDirectory = output,
            OutputStructure = OutputStructure.YearMonth,
            WriteMetadata = false,
            CpuParallelism = Math.Max(4, Environment.ProcessorCount),
        });

        Assert.Equal(0, report.Errors);
        Assert.Equal(n, report.TotalMedia);
        Assert.Equal(0, report.Duplicates); // all distinct content

        var monthDir = Path.Combine(output, "ALL_PHOTOS", "2023", "2023-08");
        var produced = Directory.EnumerateFiles(monthDir, "IMG_1234*.jpg").Count();
        Assert.Equal(n, produced); // every distinct photo survived, uniquely named
        Assert.True(Directory.GetFiles(monthDir, "*.part").Length == 0, "no leftover temp files");
    }

    [Fact]
    public async Task Pipeline_HighParallelism_ManyDuplicates_KeepsOneCanonical()
    {
        // The same photo repeated across many albums: exactly one canonical copy is
        // kept; the rest are de-duplicated regardless of which thread wins.
        const int copies = 24;
        const long epoch = 1692108336;
        var photo = new byte[40_000];
        new Random(5).NextBytes(photo);

        var zipPath = Path.Combine(_dir, "takeout-001.zip");
        using (var zip = ZipFile.Open(zipPath, ZipArchiveMode.Create))
        {
            AddBinary(zip, "Takeout/Google Photos/Photos from 2023/IMG_777.jpg", photo);
            AddText(zip, "Takeout/Google Photos/Photos from 2023/IMG_777.jpg.supplemental-metadata.json", Sidecar(epoch));
            for (var i = 0; i < copies; i++)
                AddBinary(zip, $"Takeout/Google Photos/Album{i:D2}/IMG_777.jpg", photo);
        }

        var output = Path.Combine(_dir, "out2");
        var report = await new ProcessingPipeline(null).RunAsync(new ProcessingOptions
        {
            InputZipPaths = new[] { zipPath },
            OutputDirectory = output,
            OutputStructure = OutputStructure.YearMonth,
            DuplicateHandling = DuplicateHandling.KeepBest,
            AlbumStrategy = AlbumStrategy.Nothing, // don't depend on symlink privileges
            WriteMetadata = false,
            CpuParallelism = Math.Max(4, Environment.ProcessorCount),
        });

        Assert.Equal(0, report.Errors);
        Assert.Equal(copies + 1, report.TotalMedia);
        Assert.Equal(copies, report.Duplicates);

        var monthDir = Path.Combine(output, "ALL_PHOTOS", "2023", "2023-08");
        Assert.Single(Directory.EnumerateFiles(monthDir, "IMG_777*.jpg"));
    }

    [Fact]
    public async Task Pipeline_Cancelled_ReturnsReportInsteadOfThrowing()
    {
        var zipPath = Path.Combine(_dir, "takeout-001.zip");
        using (var zip = ZipFile.Open(zipPath, ZipArchiveMode.Create))
        {
            for (var i = 0; i < 5; i++)
                AddBinary(zip, $"Takeout/Google Photos/Photos from 2023/IMG_{i}.jpg", new byte[] { (byte)i });
        }

        using var cts = new CancellationTokenSource();
        cts.Cancel(); // pre-cancelled: the processing loop is abandoned immediately

        var report = await new ProcessingPipeline(null).RunAsync(new ProcessingOptions
        {
            InputZipPaths = new[] { zipPath },
            OutputDirectory = Path.Combine(_dir, "out3"),
            WriteMetadata = false,
        }, progress: null, ct: cts.Token);

        Assert.True(report.Cancelled);
    }

    private static void AddBinary(ZipArchive zip, string path, byte[] content)
    {
        var entry = zip.CreateEntry(path);
        using var s = entry.Open();
        s.Write(content);
    }

    private static void AddText(ZipArchive zip, string path, string content)
    {
        var entry = zip.CreateEntry(path);
        using var s = entry.Open();
        s.Write(Encoding.UTF8.GetBytes(content));
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { /* ignore */ }
    }
}
