using System.IO.Compression;
using System.Text;
using GPhotosTakeout.Core.Models;
using GPhotosTakeout.Core.Pipeline;
using Xunit;

namespace GPhotosTakeout.Tests;

/// <summary>
/// Real end-to-end test of the ExifTool write path. Skips automatically when exiftool.exe
/// isn't present (so the suite stays green on machines without it), but verifies the actual
/// metadata-writing pipeline when it is — the path that was previously only arg-tested.
/// </summary>
public class ExifToolIntegrationTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "gphexif_" + Guid.NewGuid().ToString("N"));

    // A tiny valid 16x16 JPEG so ExifTool has a real container to write EXIF into.
    private const string TinyJpegBase64 =
        "/9j/4AAQSkZJRgABAQEAYABgAAD/2wBDAAMCAgMCAgMDAwMEAwMEBQgFBQQEBQoHBwYIDAoMDAsKCwsNDhIQDQ4RDgsLEBYQERMUFRUVDA8XGBYUGBIUFRT/" +
        "2wBDAQMEBAUEBQkFBQkUDQsNFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBT/wAARCAAQABADASIAAhEBAxEB/8QA" +
        "HwAAAQUBAQEBAQEAAAAAAAAAAAECAwQFBgcICQoL/8QAtRAAAgEDAwIEAwUFBAQAAAF9AQIDAAQRBRIhMUEGE1FhByJxFDKBkaEII0KxwRVS0fAkM2JyggkK" +
        "FhcYGRolJicoKSo0NTY3ODk6Q0RFRkdISUpTVFVWV1hZWmNkZWZnaGlqc3R1dnd4eXqDhIWGh4iJipKTlJWWl5iZmqKjpKWmp6ipqrKztLW2t7i5usLDxMXG" +
        "x8jJytLT1NXW19jZ2uHi4+Tl5ufo6erx8vP09fb3+Pn6/8QAHwEAAwEBAQEBAQEBAQAAAAAAAAECAwQFBgcICQoL/8QAtREAAgECBAQDBAcFBAQAAQJ3AAEC" +
        "AxEEBSExBhJBUQdhcRMiMoEIFEKRobHBCSMzUvAVYnLRChYkNOEl8RcYGRomJygpKjU2Nzg5OkNERUZHSElKU1RVVldYWVpjZGVmZ2hpanN0dXZ3eHl6goOE" +
        "hYaHiImKkpOUlZaXmJmaoqOkpaanqKmqsrO0tba3uLm6wsPExcbHyMnK0tPU1dbX2Nna4uPk5ebn6Onq8vP09fb3+Pn6/9oADAMBAAIRAxEAPwD1Wiiivwc/" +
        "rU//2Q==";

    public ExifToolIntegrationTests() => Directory.CreateDirectory(_dir);

    private static string? FindExifTool()
    {
        // The App's in-app installer target (same place the App and CLI probe first).
        var installed = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "GPhotosTakeout", "Tools", "exiftool.exe");
        if (File.Exists(installed))
            return installed;

        // Walk up from the test output to the repo root and look in src/.../Tools.
        var dir = AppContext.BaseDirectory;
        for (var i = 0; i < 8 && dir is not null; i++)
        {
            var candidate = Path.Combine(dir, "src", "GPhotosTakeout.App", "Tools", "exiftool.exe");
            if (File.Exists(candidate))
                return candidate;
            dir = Directory.GetParent(dir)?.FullName;
        }
        return null;
    }

    [Fact]
    public async Task WritesDateTimeGpsAndDescription_IntoRealJpeg()
    {
        var exifTool = FindExifTool();
        if (exifTool is null)
            return; // exiftool.exe not present (e.g. CI without it) — nothing to verify here.

        var jpeg = Convert.FromBase64String(TinyJpegBase64);
        var zipPath = Path.Combine(_dir, "takeout-001.zip");
        using (var zip = ZipFile.Open(zipPath, ZipArchiveMode.Create))
        {
            Add(zip, "Takeout/Google Photos/Photos from 2021/IMG_0001.jpg", jpeg);
            Add(zip, "Takeout/Google Photos/Photos from 2021/IMG_0001.jpg.supplemental-metadata.json",
                Encoding.UTF8.GetBytes(
                    "{\"photoTakenTime\":{\"timestamp\":\"1625356800\"},\"description\":\"חוף תל אביב\"," +
                    "\"geoData\":{\"latitude\":32.0853,\"longitude\":34.7818,\"altitude\":12.0}}"));
        }

        var options = new ProcessingOptions
        {
            InputZipPaths = new[] { zipPath },
            OutputDirectory = Path.Combine(_dir, "out"),
            WriteMetadata = true,
            CpuParallelism = 1,
        };

        var report = await new ProcessingPipeline(exifTool).RunAsync(options);

        Assert.Equal(0, report.Errors);
        Assert.Equal(1, report.MetadataWritten);

        var outFile = Directory.EnumerateFiles(options.OutputDirectory, "IMG_0001.jpg", SearchOption.AllDirectories)
            .Single();
        // ExifTool actually rewrote the file (it grows once EXIF/XMP is embedded).
        Assert.True(new FileInfo(outFile).Length > jpeg.Length);
    }

    [Fact]
    public async Task AlbumDuplicate_CopiedAfterMetadataWritten()
    {
        // Regression: the dedup owner published its path before ExifTool wrote to it, so
        // a waiting album duplicate (Duplicate strategy, or Shortcut falling back to a
        // hardlink — ExifTool's -overwrite_original rewrites via temp+rename, which
        // detaches a hardlink) copied the untagged file. Several identical album copies
        // widen the race window so the pre-fix behaviour shows up reliably.
        var exifTool = FindExifTool();
        if (exifTool is null)
            return;

        var jpeg = Convert.FromBase64String(TinyJpegBase64);
        var sidecar = Encoding.UTF8.GetBytes("{\"photoTakenTime\":{\"timestamp\":\"1625356800\"}}");
        var zipPath = Path.Combine(_dir, "takeout-001.zip");
        using (var zip = ZipFile.Open(zipPath, ZipArchiveMode.Create))
        {
            Add(zip, "Takeout/Google Photos/Photos from 2021/IMG_0001.jpg", jpeg);
            Add(zip, "Takeout/Google Photos/Photos from 2021/IMG_0001.jpg.json", sidecar);
            for (var i = 0; i < 6; i++)
            {
                Add(zip, $"Takeout/Google Photos/Album {i}/IMG_0001.jpg", jpeg);
                Add(zip, $"Takeout/Google Photos/Album {i}/IMG_0001.jpg.json", sidecar);
            }
        }

        var output = Path.Combine(_dir, "out");
        var report = await new ProcessingPipeline(exifTool).RunAsync(new ProcessingOptions
        {
            InputZipPaths = new[] { zipPath },
            OutputDirectory = output,
            AlbumStrategy = AlbumStrategy.Duplicate,
            WriteMetadata = true,
            CpuParallelism = 4,
        });

        Assert.Equal(0, report.Errors);
        Assert.Equal(6, report.Duplicates);

        var canonical = new FileInfo(Directory.EnumerateFiles(
            Path.Combine(output, OutputPathBuilder.AllPhotos), "IMG_0001.jpg", SearchOption.AllDirectories).Single());
        Assert.True(canonical.Length > jpeg.Length, "canonical must carry metadata");
        foreach (var copy in Directory.EnumerateFiles(Path.Combine(output, "Albums"), "IMG_0001.jpg", SearchOption.AllDirectories))
            Assert.Equal(canonical.Length, new FileInfo(copy).Length);
    }

    private static void Add(ZipArchive zip, string path, byte[] bytes)
    {
        var e = zip.CreateEntry(path);
        using var s = e.Open();
        s.Write(bytes);
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { /* ignore */ }
    }
}
