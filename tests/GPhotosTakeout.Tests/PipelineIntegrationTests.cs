using System.IO.Compression;
using System.Text;
using GPhotosTakeout.Core.Models;
using GPhotosTakeout.Core.Pipeline;
using Xunit;

namespace GPhotosTakeout.Tests;

public class PipelineIntegrationTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "gphpipe_" + Guid.NewGuid().ToString("N"));

    public PipelineIntegrationTests() => Directory.CreateDirectory(_dir);

    private static string Sidecar(long epoch) =>
        "{\"photoTakenTime\":{\"timestamp\":\"" + epoch + "\"},\"geoData\":{\"latitude\":0,\"longitude\":0}}";

    [Fact]
    public async Task Run_OrganizesByYearMonth_DedupesAlbumCopy_ReportsUnmatched()
    {
        // Build a synthetic Takeout zip.
        var zipPath = Path.Combine(_dir, "takeout-001.zip");
        var photoBytes = new byte[50_000];
        new Random(7).NextBytes(photoBytes);
        const long epoch = 1692108336; // 2023-08-15 UTC

        using (var zip = ZipFile.Open(zipPath, ZipArchiveMode.Create))
        {
            AddBinary(zip, "Takeout/Google Photos/Photos from 2023/IMG_1234.jpg", photoBytes);
            AddText(zip, "Takeout/Google Photos/Photos from 2023/IMG_1234.jpg.supplemental-metadata.json", Sidecar(epoch));
            // Same photo also in an album (duplicate content) -> should dedupe + link.
            AddBinary(zip, "Takeout/Google Photos/Trip to Eilat/IMG_1234.jpg", photoBytes);
            AddText(zip, "Takeout/Google Photos/Trip to Eilat/IMG_1234.jpg.supplemental-metadata.json", Sidecar(epoch));
            // A media file with no sidecar -> unmatched but still placed.
            AddBinary(zip, "Takeout/Google Photos/Photos from 2023/orphan.jpg", new byte[] { 9, 9, 9 });
        }

        var output = Path.Combine(_dir, "out");
        var options = new ProcessingOptions
        {
            InputZipPaths = new[] { zipPath },
            OutputDirectory = output,
            OutputStructure = OutputStructure.YearMonth,
            WriteMetadata = false,       // no ExifTool needed for this test
            CpuParallelism = 1,          // deterministic dedup ordering
        };

        // exifToolPath null -> metadata writing skipped.
        var report = await new ProcessingPipeline(null).RunAsync(options);

        // 3 media files total (2 IMG_1234 + orphan); one IMG is a duplicate.
        Assert.Equal(3, report.TotalMedia);
        Assert.Equal(1, report.Duplicates);
        Assert.Equal(0, report.Errors);

        // Canonical copy organized by capture date.
        Assert.True(File.Exists(Path.Combine(output, "ALL_PHOTOS", "2023", "2023-08", "IMG_1234.jpg")),
            "canonical photo should be placed under year/month");

        // Orphan still placed; with no JSON its date falls back to the "Photos from
        // 2023" folder year, so it lands somewhere under ALL_PHOTOS/2023.
        var orphan = Directory.EnumerateFiles(Path.Combine(output, "ALL_PHOTOS", "2023"), "orphan.jpg",
            SearchOption.AllDirectories).FirstOrDefault();
        Assert.True(orphan is not null, "unmatched photo should still be extracted");
    }

    [Fact]
    public async Task Run_NonMediaFiles_AreSkippedNotOrganized()
    {
        // Regression: a run whose input contained arbitrary non-media files (DLLs,
        // Perl modules from an ExifTool install, files with no extension) used to
        // organize them into the photo library and feed them to ExifTool. They must
        // be skipped entirely — not extracted, not counted as media.
        var zipPath = Path.Combine(_dir, "takeout-001.zip");
        const long epoch = 1692108336; // 2023-08-15 UTC

        using (var zip = ZipFile.Open(zipPath, ZipArchiveMode.Create))
        {
            AddBinary(zip, "Takeout/Google Photos/Photos from 2023/IMG_1234.jpg", new byte[] { 1, 2, 3 });
            AddText(zip, "Takeout/Google Photos/Photos from 2023/IMG_1234.jpg.supplemental-metadata.json", Sidecar(epoch));
            AddBinary(zip, "Takeout/Google Photos/Photos from 2023/Microsoft.ui.xaml.dll", new byte[] { 4 });
            AddBinary(zip, "Takeout/Google Photos/Photos from 2023/Exif.pm", new byte[] { 5 });
            AddBinary(zip, "Takeout/Google Photos/Photos from 2023/README", new byte[] { 6 });
        }

        var output = Path.Combine(_dir, "out-nonmedia");
        var report = await new ProcessingPipeline(null).RunAsync(new ProcessingOptions
        {
            InputZipPaths = new[] { zipPath },
            OutputDirectory = output,
            OutputStructure = OutputStructure.YearMonth,
            WriteMetadata = false,
            CpuParallelism = 1,
        });

        Assert.Equal(1, report.TotalMedia);
        Assert.Equal(3, report.SkippedNonMedia);
        Assert.Equal(0, report.Errors);
        Assert.True(File.Exists(Path.Combine(output, "ALL_PHOTOS", "2023", "2023-08", "IMG_1234.jpg")));
        Assert.Empty(Directory.EnumerateFiles(output, "*.dll", SearchOption.AllDirectories));
        Assert.Empty(Directory.EnumerateFiles(output, "*.pm", SearchOption.AllDirectories));
        Assert.Empty(Directory.EnumerateFiles(output, "README", SearchOption.AllDirectories));
    }

    [Fact]
    public async Task Run_Resume_SkipsAlreadyDoneFiles()
    {
        var zipPath = Path.Combine(_dir, "takeout-001.zip");
        using (var zip = ZipFile.Open(zipPath, ZipArchiveMode.Create))
        {
            AddBinary(zip, "Takeout/Google Photos/Photos from 2023/A.jpg", new byte[] { 1 });
            AddText(zip, "Takeout/Google Photos/Photos from 2023/A.jpg.supplemental-metadata.json", Sidecar(1692108336));
        }

        var output = Path.Combine(_dir, "out2");
        var options = new ProcessingOptions
        {
            InputZipPaths = new[] { zipPath },
            OutputDirectory = output,
            WriteMetadata = false,
            CpuParallelism = 1,
        };

        await new ProcessingPipeline(null).RunAsync(options);
        // Second run: journal marks everything done, so nothing reprocesses (no errors).
        var second = await new ProcessingPipeline(null).RunAsync(options);
        Assert.Equal(0, second.Errors);

        Assert.True(File.Exists(Path.Combine(output, ResumeJournal.FileName)));
    }

    [Fact]
    public async Task Run_NoOtherDateSource_FallsBackToZipEntryModifiedTime()
    {
        var zipPath = Path.Combine(_dir, "takeout-001.zip");
        using (var zip = ZipFile.Open(zipPath, ZipArchiveMode.Create))
        {
            // No sidecar, no date in the name, no year in the folder -> only the
            // ZIP entry's last-write time can date this file.
            var entry = zip.CreateEntry("Takeout/Google Photos/Random Stuff/holiday.jpg");
            entry.LastWriteTime = new DateTimeOffset(2019, 3, 10, 12, 0, 0, TimeSpan.Zero);
            using var s = entry.Open();
            s.Write(new byte[] { 1, 2, 3 });
        }

        var output = Path.Combine(_dir, "out3");
        var options = new ProcessingOptions
        {
            InputZipPaths = new[] { zipPath },
            OutputDirectory = output,
            WriteMetadata = false,
            CpuParallelism = 1,
        };

        var report = await new ProcessingPipeline(null).RunAsync(options);

        Assert.Equal(0, report.Errors);
        var outcome = Assert.Single(report.Outcomes);
        Assert.Equal("FileModified", outcome.DateSource);

        var placed = Directory.EnumerateFiles(Path.Combine(output, "ALL_PHOTOS", "2019"), "holiday.jpg",
            SearchOption.AllDirectories).FirstOrDefault();
        Assert.True(placed is not null, "file should be dated from the ZIP entry timestamp");
    }

    [Fact]
    public async Task Run_NoSidecarDate_UsesExifDateFromFile()
    {
        var zipPath = Path.Combine(_dir, "takeout-001.zip");
        using (var zip = ZipFile.Open(zipPath, ZipArchiveMode.Create))
        {
            // No sidecar, no date in the name — but the JPEG carries DateTimeOriginal.
            // The entry timestamp is deliberately different to prove EXIF outranks it.
            var entry = zip.CreateEntry("Takeout/Google Photos/Random Stuff/photo.jpg");
            entry.LastWriteTime = new DateTimeOffset(2024, 1, 1, 12, 0, 0, TimeSpan.Zero);
            using var s = entry.Open();
            s.Write(TestJpeg.WithDateTimeOriginal("2021:05:04 10:20:30"));
        }

        var output = Path.Combine(_dir, "out4");
        var options = new ProcessingOptions
        {
            InputZipPaths = new[] { zipPath },
            OutputDirectory = output,
            WriteMetadata = false,
            CpuParallelism = 1,
        };

        var report = await new ProcessingPipeline(null).RunAsync(options);

        Assert.Equal(0, report.Errors);
        var outcome = Assert.Single(report.Outcomes);
        Assert.Equal("Exif", outcome.DateSource);
        Assert.True(File.Exists(Path.Combine(output, "ALL_PHOTOS", "2021", "2021-05", "photo.jpg")),
            "file should be dated from its embedded EXIF capture date");
    }

    [Fact]
    public async Task Run_ExifFallbackDisabled_FallsBackToModifiedTime()
    {
        var zipPath = Path.Combine(_dir, "takeout-001.zip");
        using (var zip = ZipFile.Open(zipPath, ZipArchiveMode.Create))
        {
            var entry = zip.CreateEntry("Takeout/Google Photos/Random Stuff/photo.jpg");
            entry.LastWriteTime = new DateTimeOffset(2024, 1, 1, 12, 0, 0, TimeSpan.Zero);
            using var s = entry.Open();
            s.Write(TestJpeg.WithDateTimeOriginal("2021:05:04 10:20:30"));
        }

        var output = Path.Combine(_dir, "out5");
        var report = await new ProcessingPipeline(null).RunAsync(new ProcessingOptions
        {
            InputZipPaths = new[] { zipPath },
            OutputDirectory = output,
            WriteMetadata = false,
            UseExifFallback = false,
            CpuParallelism = 1,
        });

        Assert.Equal(0, report.Errors);
        var outcome = Assert.Single(report.Outcomes);
        Assert.Equal("FileModified", outcome.DateSource);
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
