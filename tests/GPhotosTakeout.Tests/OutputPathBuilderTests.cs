using GPhotosTakeout.Core.Models;
using GPhotosTakeout.Core.Pipeline;
using Xunit;

namespace GPhotosTakeout.Tests;

public class OutputPathBuilderTests
{
    private static TakeoutEntry Media(string path) =>
        new() { Path = path, ArchiveId = "a.zip", Length = 1 };

    [Fact]
    public void YearMonth_GroupsByCaptureDate()
    {
        var builder = new OutputPathBuilder(OutputStructure.YearMonth);
        var p = builder.BuildPath(@"C:\out", Media("Takeout/Google Photos/Photos from 2023/IMG.jpg"),
            new DateTime(2023, 8, 15));
        Assert.Equal(@"C:\out\ALL_PHOTOS\2023\2023-08\IMG.jpg", p);
    }

    [Fact]
    public void YearMonth_UndatedWhenNoDate()
    {
        var builder = new OutputPathBuilder(OutputStructure.YearMonth);
        var p = builder.BuildPath(@"C:\out", Media("a/IMG.jpg"), null);
        Assert.Equal(@"C:\out\ALL_PHOTOS\Undated\IMG.jpg", p);
    }

    [Fact]
    public void SpecialFolders_AreSegregated()
    {
        var builder = new OutputPathBuilder(OutputStructure.YearMonth);
        var archive = builder.BuildPath(@"C:\out", Media("Takeout/Google Photos/Archive/IMG.jpg"), new DateTime(2023, 1, 1));
        Assert.Equal(@"C:\out\Archive\IMG.jpg", archive);
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
}
