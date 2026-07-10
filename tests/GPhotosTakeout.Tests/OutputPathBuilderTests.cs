using GPhotosTakeout.Core.Models;
using GPhotosTakeout.Core.Pipeline;
using Xunit;

namespace GPhotosTakeout.Tests;

public class OutputPathBuilderTests
{
    private static TakeoutEntry Media(string path) =>
        new() { Path = path, ArchiveId = "a.zip", Length = 1 };

    // Builds the expected path with Path.Combine so the assertions hold on any
    // OS separator (the builder itself uses Path.Combine).
    private static string P(params string[] parts) => Path.Combine(parts);

    [Fact]
    public void YearMonth_GroupsByCaptureDate()
    {
        var builder = new OutputPathBuilder(OutputStructure.YearMonth);
        var p = builder.BuildPath(@"C:\out", Media("Takeout/Google Photos/Photos from 2023/IMG.jpg"),
            new DateTime(2023, 8, 15));
        Assert.Equal(P(@"C:\out", "ALL_PHOTOS", "2023", "2023-08", "IMG.jpg"), p);
    }

    [Fact]
    public void YearMonth_UndatedWhenNoDate()
    {
        var builder = new OutputPathBuilder(OutputStructure.YearMonth);
        var p = builder.BuildPath(@"C:\out", Media("a/IMG.jpg"), null);
        Assert.Equal(P(@"C:\out", "ALL_PHOTOS", "Undated", "IMG.jpg"), p);
    }

    [Fact]
    public void SpecialFolders_AreSegregated()
    {
        var builder = new OutputPathBuilder(OutputStructure.YearMonth);
        var archive = builder.BuildPath(@"C:\out", Media("Takeout/Google Photos/Archive/IMG.jpg"), new DateTime(2023, 1, 1));
        Assert.Equal(P(@"C:\out", "Archive", "IMG.jpg"), archive);
    }

    [Theory]
    [InlineData("Takeout/Google Photos/Photos from 2023", true)]
    [InlineData("Takeout/Google Photos/Trip to Eilat", false)]
    public void IsMainLibraryFolder_DetectsYearFolders(string folder, bool expected)
    {
        Assert.Equal(expected, OutputPathBuilder.IsMainLibraryFolder(folder));
    }

    [Fact]
    public void Classify_DetectsSpecialFolders()
    {
        Assert.Equal(SpecialFolder.Archive, OutputPathBuilder.Classify("x/Archive"));
        Assert.Equal(SpecialFolder.Trash, OutputPathBuilder.Classify("x/Trash"));
        Assert.Equal(SpecialFolder.None, OutputPathBuilder.Classify("x/Photos from 2023"));
    }

    // ── Flat structure ───────────────────────────────────────────────────────

    [Fact]
    public void Flat_PlacesAllFilesUnderAllPhotos()
    {
        var builder = new OutputPathBuilder(OutputStructure.Flat);
        var p = builder.BuildPath(@"C:\out", Media("Takeout/Google Photos/Photos from 2023/IMG.jpg"),
            new DateTime(2023, 8, 15));
        Assert.Equal(P(@"C:\out", "ALL_PHOTOS", "IMG.jpg"), p);
    }

    [Fact]
    public void Flat_AlbumFolderAlsoLandsInAllPhotos()
    {
        var builder = new OutputPathBuilder(OutputStructure.Flat);
        var p = builder.BuildPath(@"C:\out", Media("Takeout/Google Photos/SummerTrip/IMG.jpg"), null);
        Assert.Equal(P(@"C:\out", "ALL_PHOTOS", "IMG.jpg"), p);
    }

    [Fact]
    public void Flat_IgnoresDate_SamePathWithOrWithoutDate()
    {
        var builder = new OutputPathBuilder(OutputStructure.Flat);
        var withDate = builder.BuildPath(@"C:\out", Media("x/IMG.jpg"), new DateTime(2023, 1, 1));
        var noDate   = builder.BuildPath(@"C:\out", Media("x/IMG.jpg"), null);
        Assert.Equal(withDate, noDate);
    }

    // ── Albums structure ─────────────────────────────────────────────────────

    [Fact]
    public void Albums_PlacesFileInAlbumNamedFolder()
    {
        var builder = new OutputPathBuilder(OutputStructure.Albums);
        var p = builder.BuildPath(@"C:\out", Media("Takeout/Google Photos/My Trip/IMG.jpg"), null);
        Assert.Equal(P(@"C:\out", "My Trip", "IMG.jpg"), p);
    }

    [Fact]
    public void Albums_MainLibraryFolderUsedAsAlbumName()
    {
        var builder = new OutputPathBuilder(OutputStructure.Albums);
        var p = builder.BuildPath(@"C:\out", Media("Takeout/Google Photos/Photos from 2023/IMG.jpg"),
            new DateTime(2023, 8, 15));
        // "Photos from 2023" is just a folder name in Albums mode.
        Assert.Equal(P(@"C:\out", "Photos from 2023", "IMG.jpg"), p);
    }

    // ── Special folder edge cases ────────────────────────────────────────────

    [Fact]
    public void SpecialFolder_Bin_ClassifiedAsTrash()
    {
        Assert.Equal(SpecialFolder.Trash, OutputPathBuilder.Classify("Takeout/Google Photos/Bin"));
    }

    [Fact]
    public void SpecialFolder_LockedFolder_Detected()
    {
        Assert.Equal(SpecialFolder.LockedFolder, OutputPathBuilder.Classify("Takeout/Google Photos/Locked Folder"));
    }

    [Fact]
    public void SpecialFolder_LockedFolder_PlacedInOwnSubdir()
    {
        var builder = new OutputPathBuilder(OutputStructure.YearMonth);
        var p = builder.BuildPath(@"C:\out",
            Media("Takeout/Google Photos/Locked Folder/secret.jpg"),
            new DateTime(2023, 1, 1));
        // Special folders bypass the ALL_PHOTOS tree entirely.
        Assert.StartsWith(P(@"C:\out", "LockedFolder") + Path.DirectorySeparatorChar, p, StringComparison.Ordinal);
    }

    [Fact]
    public void SpecialFolder_Trash_PlacedInOwnSubdir()
    {
        var builder = new OutputPathBuilder(OutputStructure.YearMonth);
        var p = builder.BuildPath(@"C:\out",
            Media("Takeout/Google Photos/Trash/deleted.jpg"),
            new DateTime(2023, 1, 1));
        Assert.StartsWith(P(@"C:\out", "Trash") + Path.DirectorySeparatorChar, p, StringComparison.Ordinal);
    }
}
