using GPhotosTakeout.Core.Dedup;
using Xunit;

namespace GPhotosTakeout.Tests;

public class HashDeduplicatorTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "gphdedup_" + Guid.NewGuid().ToString("N"));

    public HashDeduplicatorTests() => Directory.CreateDirectory(_dir);

    private string WriteFile(string name, byte[] content)
    {
        var path = Path.Combine(_dir, name);
        File.WriteAllBytes(path, content);
        return path;
    }

    [Fact]
    public void IdenticalContent_IsDetected()
    {
        var content = new byte[100_000];
        new Random(1).NextBytes(content);
        var a = WriteFile("a.bin", content);
        var b = WriteFile("b.bin", (byte[])content.Clone());

        var dedup = new HashDeduplicator();
        Assert.Null(dedup.FindDuplicateOrRegister(a));
        Assert.Equal(a, dedup.FindDuplicateOrRegister(b));
    }

    [Fact]
    public void DifferentContent_IsNotDuplicate()
    {
        var dedup = new HashDeduplicator();
        var a = WriteFile("a.bin", new byte[] { 1, 2, 3 });
        var b = WriteFile("b.bin", new byte[] { 4, 5, 6 });
        Assert.Null(dedup.FindDuplicateOrRegister(a));
        Assert.Null(dedup.FindDuplicateOrRegister(b));
    }

    [Fact]
    public void SameSizeDifferentMiddle_FullHashDistinguishes()
    {
        // Same length, identical head/tail, differing middle: quick sig may collide,
        // full hash must still tell them apart.
        var x = new byte[200_000];
        var y = new byte[200_000];
        x[100_000] = 1;
        y[100_000] = 2;
        var a = WriteFile("a.bin", x);
        var b = WriteFile("b.bin", y);

        var dedup = new HashDeduplicator();
        Assert.Null(dedup.FindDuplicateOrRegister(a));
        Assert.Null(dedup.FindDuplicateOrRegister(b));
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { /* ignore */ }
    }
}
