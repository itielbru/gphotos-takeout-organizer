using GPhotosTakeout.Core.IO;
using Xunit;

namespace GPhotosTakeout.Tests;

public class LongPathTests
{
    [Fact]
    public void Extended_PrefixesFullyQualifiedPath()
    {
        Assert.Equal(@"\\?\C:\a\b.jpg", LongPath.Extended(@"C:\a\b.jpg"));
    }

    [Fact]
    public void Extended_NormalizesForwardSlashes()
    {
        // "\\?\" paths are used verbatim by the OS, so '/' must become '\'.
        Assert.Equal(@"\\?\C:\a\b.jpg", LongPath.Extended("C:/a/b.jpg"));
    }

    [Fact]
    public void Extended_HandlesUncPaths()
    {
        Assert.Equal(@"\\?\UNC\server\share\f.jpg", LongPath.Extended(@"\\server\share\f.jpg"));
        Assert.Equal(@"\\?\UNC\server\share\f.jpg", LongPath.Extended("//server/share/f.jpg"));
    }

    [Fact]
    public void Extended_LeavesAlreadyExtendedUnchanged()
    {
        Assert.Equal(@"\\?\C:\x.jpg", LongPath.Extended(@"\\?\C:\x.jpg"));
    }

    [Fact]
    public void Extended_LeavesRelativePathUnchanged()
    {
        Assert.Equal(@"sub\f.jpg", LongPath.Extended(@"sub\f.jpg"));
    }

    [Fact]
    public void RoundTrips_Create_Read_Move_Delete_WithForwardSlashes()
    {
        var dir = Path.Combine(Path.GetTempPath(), "gphlp_" + Guid.NewGuid().ToString("N")).Replace('\\', '/');
        try
        {
            var file = dir + "/nested/deep/file.txt";
            using (var s = LongPath.Create(file))
                s.Write("hi"u8);

            Assert.True(LongPath.Exists(file));

            var moved = dir + "/nested/deep/moved.txt";
            LongPath.Move(file, moved);
            Assert.True(LongPath.Exists(moved));
            Assert.False(LongPath.Exists(file));

            LongPath.Delete(moved);
            Assert.False(LongPath.Exists(moved));
        }
        finally
        {
            try { Directory.Delete(dir.Replace('/', '\\'), recursive: true); } catch { /* ignore */ }
        }
    }
}
