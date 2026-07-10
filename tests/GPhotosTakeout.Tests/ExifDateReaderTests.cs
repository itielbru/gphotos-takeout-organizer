using GPhotosTakeout.Core.Metadata;
using Xunit;

namespace GPhotosTakeout.Tests;

public class ExifDateReaderTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "gphexifread_" + Guid.NewGuid().ToString("N"));

    public ExifDateReaderTests() => Directory.CreateDirectory(_dir);

    [Fact]
    public void TryRead_JpegWithDateTimeOriginal_ReturnsLocalWallClock()
    {
        var path = Write("dated.jpg", TestJpeg.WithDateTimeOriginal("2021:05:04 10:20:30"));

        var result = ExifDateReader.TryRead(path);

        Assert.NotNull(result);
        Assert.Equal(new DateTime(2021, 5, 4, 10, 20, 30), result.Value.Value);
        Assert.False(result.Value.IsUtc, "EXIF DateTimeOriginal is naive local wall-clock");
    }

    [Fact]
    public void TryRead_JpegWithoutExifDate_ReturnsNull()
    {
        // Valid JPEG markers but no APP1/EXIF segment at all.
        var path = Write("bare.jpg", new byte[] { 0xFF, 0xD8, 0xFF, 0xD9 });
        Assert.Null(ExifDateReader.TryRead(path));
    }

    [Fact]
    public void TryRead_ImplausibleExifDate_ReturnsNull()
    {
        // Cameras with a dead clock write epoch-ish garbage; the reader rejects it.
        var path = Write("epoch.jpg", TestJpeg.WithDateTimeOriginal("1970:01:01 00:00:00"));
        Assert.Null(ExifDateReader.TryRead(path));
    }

    [Fact]
    public void TryRead_GarbageBytes_ReturnsNullInsteadOfThrowing()
    {
        var path = Write("garbage.bin", new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 });
        Assert.Null(ExifDateReader.TryRead(path));
    }

    [Fact]
    public void TryRead_MissingFile_ReturnsNullInsteadOfThrowing()
    {
        Assert.Null(ExifDateReader.TryRead(Path.Combine(_dir, "does-not-exist.jpg")));
    }

    private string Write(string name, byte[] bytes)
    {
        var path = Path.Combine(_dir, name);
        File.WriteAllBytes(path, bytes);
        return path;
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { /* ignore */ }
    }
}

/// <summary>
/// Builds a minimal-but-valid JPEG whose only content is an EXIF APP1 segment with a
/// DateTimeOriginal tag — enough for a metadata reader, no decodable image data needed.
/// </summary>
internal static class TestJpeg
{
    public static byte[] WithDateTimeOriginal(string exifDateTime) // "yyyy:MM:dd HH:mm:ss"
    {
        // TIFF little-endian block: IFD0 with one ExifIFD pointer -> ExifIFD with one
        // ASCII DateTimeOriginal (tag 0x9003) pointing at the 20-byte date string.
        var date = System.Text.Encoding.ASCII.GetBytes(exifDateTime + "\0");
        if (date.Length != 20)
            throw new ArgumentException("EXIF date must be exactly 19 chars", nameof(exifDateTime));

        using var tiff = new MemoryStream();
        void U16(ushort v) { tiff.WriteByte((byte)v); tiff.WriteByte((byte)(v >> 8)); }
        void U32(uint v) { U16((ushort)v); U16((ushort)(v >> 16)); }

        tiff.Write("II"u8);        // little-endian
        U16(42);                   // TIFF magic
        U32(8);                    // IFD0 offset

        U16(1);                    // IFD0: one entry
        U16(0x8769); U16(4); U32(1); U32(26); // ExifIFD pointer (LONG) -> offset 26
        U32(0);                    // no next IFD

        U16(1);                    // ExifIFD (at 26): one entry
        U16(0x9003); U16(2); U32(20); U32(44); // DateTimeOriginal (ASCII, 20 bytes) -> offset 44
        U32(0);                    // no next IFD

        tiff.Write(date);          // at offset 44

        var payload = "Exif\0\0"u8.ToArray().Concat(tiff.ToArray()).ToArray();
        var app1Len = (ushort)(payload.Length + 2);

        using var jpeg = new MemoryStream();
        jpeg.Write([0xFF, 0xD8]);                                    // SOI
        jpeg.Write([0xFF, 0xE1, (byte)(app1Len >> 8), (byte)app1Len]); // APP1 marker + length
        jpeg.Write(payload);
        jpeg.Write([0xFF, 0xD9]);                                    // EOI
        return jpeg.ToArray();
    }
}
