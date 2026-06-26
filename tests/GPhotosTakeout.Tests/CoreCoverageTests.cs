using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using GPhotosTakeout.Core.Albums;
using GPhotosTakeout.Core.Archives;
using GPhotosTakeout.Core.Dates;
using GPhotosTakeout.Core.Models;
using Xunit;

namespace GPhotosTakeout.Tests;

public class TimezoneResolverCacheTests
{
    private static readonly TakeoutGeo TelAviv = new() { Latitude = 32.0853, Longitude = 34.7818 };
    private static readonly TakeoutGeo TelAvivClose = new() { Latitude = 32.0901, Longitude = 34.7850 }; // ~500m away
    private static readonly TakeoutGeo London = new() { Latitude = 51.5074, Longitude = -0.1278 };

    [Fact]
    public void SameGps_ReturnsSameZoneObject()
    {
        var resolver = new TimezoneResolver(null);
        var zone1 = resolver.ResolveZone(TelAviv);
        var zone2 = resolver.ResolveZone(TelAviv);
        Assert.Same(zone1, zone2); // cached: identical reference
    }

    [Fact]
    public void NearbyGps_RoundsToSameKey_ReturnsSameZone()
    {
        var resolver = new TimezoneResolver(null);
        // Both coords round to ~same 1-km cell, so the same cache slot is hit.
        var zone1 = resolver.ResolveZone(TelAviv);
        var zone2 = resolver.ResolveZone(TelAvivClose);
        Assert.Equal(zone1?.Id, zone2?.Id);
    }

    [Fact]
    public void DifferentGps_ReturnsDifferentZones()
    {
        var resolver = new TimezoneResolver(null);
        var zoneTelAviv = resolver.ResolveZone(TelAviv);
        var zoneLondon = resolver.ResolveZone(London);
        Assert.NotEqual(zoneTelAviv?.Id, zoneLondon?.Id);
    }

    [Fact]
    public void NullGeo_ReturnsFallback()
    {
        var resolver = new TimezoneResolver("Asia/Jerusalem");
        var zone = resolver.ResolveZone(geo: null);
        Assert.NotNull(zone);
        Assert.Equal(TimeSpan.FromHours(2), zone!.GetUtcOffset(new DateTime(2023, 1, 15, 12, 0, 0, DateTimeKind.Utc)));
    }
}

public class TimezoneResolverTests
{
    // Tel Aviv.
    private static readonly TakeoutGeo TelAviv = new() { Latitude = 32.0853, Longitude = 34.7818 };

    [Fact]
    public void Gps_Summer_ResolvesToIsraelDaylightOffset()
    {
        var tz = new TimezoneResolver(fallbackIanaId: null);
        var utc = new DateTimeOffset(2023, 8, 15, 12, 0, 0, TimeSpan.Zero); // IDT (+03:00)
        var local = tz.ResolveLocal(utc, TelAviv);
        Assert.Equal(TimeSpan.FromHours(3), local.Offset);
    }

    [Fact]
    public void Fallback_Winter_UsesIsraelStandardOffset()
    {
        var tz = new TimezoneResolver("Asia/Jerusalem");
        var utc = new DateTimeOffset(2023, 1, 15, 12, 0, 0, TimeSpan.Zero); // IST (+02:00)
        var local = tz.ResolveLocal(utc, geo: null);
        Assert.Equal(TimeSpan.FromHours(2), local.Offset);
    }

    [Fact]
    public void NoGeo_NoFallback_StaysUtc()
    {
        var tz = new TimezoneResolver(fallbackIanaId: null);
        var utc = new DateTimeOffset(2023, 6, 1, 9, 0, 0, TimeSpan.Zero);
        var local = tz.ResolveLocal(utc, geo: null);
        Assert.Equal(TimeSpan.Zero, local.Offset);
    }

    [Fact]
    public void InvalidFallback_TreatedAsNone()
    {
        var tz = new TimezoneResolver("Not/AZone");
        Assert.Null(tz.ResolveZone(geo: null));
    }

    [Fact]
    public void Gps_PicksZone_OverFallback()
    {
        var tz = new TimezoneResolver("Asia/Jerusalem");
        var zone = tz.ResolveZone(TelAviv);
        Assert.NotNull(zone);
        // Summer offset for the resolved zone is +3h regardless of the zone's display id.
        Assert.Equal(TimeSpan.FromHours(3), zone!.GetUtcOffset(new DateTime(2023, 8, 15, 12, 0, 0, DateTimeKind.Utc)));
    }
}

public class AlbumLinkerTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "gphalbum_" + Guid.NewGuid().ToString("N"));

    public AlbumLinkerTests() => Directory.CreateDirectory(_dir);

    [Fact]
    public void Link_MaterializesEntry_WithMatchingContent()
    {
        var target = Path.Combine(_dir, "canonical.jpg");
        var content = new byte[] { 1, 2, 3, 4, 5 };
        File.WriteAllBytes(target, content);

        var linkPath = Path.Combine(_dir, "Albums", "Trip", "canonical.jpg");
        var outcome = new AlbumLinker().Link(target, linkPath);

        Assert.True(outcome.Succeeded, outcome.Error);
        Assert.True(File.Exists(linkPath));
        // Whichever method won (symlink/hardlink/copy), the bytes must be readable & equal.
        Assert.Equal(content, File.ReadAllBytes(linkPath));
    }

    [Fact]
    public void Link_CreatesParentDirectories()
    {
        var target = Path.Combine(_dir, "c.jpg");
        File.WriteAllBytes(target, new byte[] { 9 });
        var linkPath = Path.Combine(_dir, "a", "b", "c", "c.jpg");

        var outcome = new AlbumLinker().Link(target, linkPath);

        Assert.True(outcome.Succeeded, outcome.Error);
        Assert.True(Directory.Exists(Path.GetDirectoryName(linkPath)));
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { /* ignore */ }
    }
}

public class TakeoutArchiveReaderTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "gpharc_" + Guid.NewGuid().ToString("N"));

    public TakeoutArchiveReaderTests() => Directory.CreateDirectory(_dir);

    private string MakeZip(string name, params (string path, byte[] bytes)[] entries)
    {
        var zipPath = Path.Combine(_dir, name);
        using var zip = ZipFile.Open(zipPath, ZipArchiveMode.Create);
        foreach (var (path, bytes) in entries)
        {
            var e = zip.CreateEntry(path);
            using var s = e.Open();
            s.Write(bytes);
        }
        return zipPath;
    }

    [Fact]
    public void Index_DerivesFileNameFolderAndSidecarFlag()
    {
        var zip = MakeZip("takeout-001.zip",
            ("Takeout/Google Photos/Photos from 2023/IMG.jpg", new byte[] { 1 }),
            ("Takeout/Google Photos/Photos from 2023/IMG.jpg.supplemental-metadata.json", "{}"u8.ToArray()));

        using var reader = new TakeoutArchiveReader();
        var entries = reader.Index(new[] { zip });

        Assert.Equal(2, entries.Count);
        var media = entries.Single(e => e.FileName == "IMG.jpg");
        Assert.Equal("Takeout/Google Photos/Photos from 2023", media.Folder);
        Assert.Equal("takeout-001.zip", media.ArchiveId);
        Assert.False(media.IsSidecar);
        Assert.Contains(entries, e => e.IsSidecar);
    }

    [Fact]
    public async Task Extract_ComputesContentHash()
    {
        var content = new byte[2000];
        new Random(11).NextBytes(content);
        var zip = MakeZip("takeout-001.zip", ("Takeout/IMG.jpg", content));

        using var reader = new TakeoutArchiveReader();
        var entry = reader.Index(new[] { zip }).Single();
        var dest = Path.Combine(_dir, "out", "IMG.jpg");

        var result = await reader.ExtractAsync(entry, dest);

        Assert.True(File.Exists(dest));
        Assert.Equal(content, File.ReadAllBytes(dest));
        Assert.Equal(Convert.ToHexString(SHA256.HashData(content)), result.ContentHash);
        Assert.Equal(content.Length, result.Length);
    }

    [Fact]
    public async Task ReadAllBytes_ReturnsSidecarContent()
    {
        var json = Encoding.UTF8.GetBytes("{\"title\":\"שלום\"}");
        var zip = MakeZip("takeout-001.zip", ("Takeout/IMG.jpg.json", json));

        using var reader = new TakeoutArchiveReader();
        var entry = reader.Index(new[] { zip }).Single();
        var bytes = await reader.ReadAllBytesAsync(entry);

        Assert.Equal(json, bytes);
    }

    [Fact]
    public void Index_MergesMultipleArchives_WithDistinctArchiveIds()
    {
        var zip1 = MakeZip("takeout-001.zip", ("Takeout/A.jpg", new byte[] { 1 }));
        var zip2 = MakeZip("takeout-002.zip", ("Takeout/B.jpg", new byte[] { 2 }));

        using var reader = new TakeoutArchiveReader();
        var entries = reader.Index(new[] { zip1, zip2 });

        Assert.Equal(2, entries.Count);
        Assert.Equal(new[] { "takeout-001.zip", "takeout-002.zip" },
            entries.Select(e => e.ArchiveId).OrderBy(x => x).ToArray());
    }

    [Fact]
    public void Index_CorruptZip_ThrowsCorruptArchiveException()
    {
        var bad = Path.Combine(_dir, "broken.zip");
        File.WriteAllText(bad, "this is not a zip file");

        using var reader = new TakeoutArchiveReader();
        var ex = Assert.Throws<CorruptArchiveException>(() => reader.Index(new[] { bad }));
        Assert.Equal(bad, ex.ArchivePath);
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { /* ignore */ }
    }
}
