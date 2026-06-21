using GPhotosTakeout.Core.Matching;
using GPhotosTakeout.Core.Models;
using Xunit;

namespace GPhotosTakeout.Tests;

public class SidecarMatcherTests
{
    private static TakeoutEntry Entry(string path, string archive = "takeout-001.zip") =>
        new() { Path = path, ArchiveId = archive, Length = 1000 };

    private readonly SidecarMatcher _matcher = new();

    [Fact]
    public void Match_ExactSupplementalMetadata()
    {
        var entries = new[]
        {
            Entry("Takeout/Google Photos/Photos from 2023/IMG_1234.jpg"),
            Entry("Takeout/Google Photos/Photos from 2023/IMG_1234.jpg.supplemental-metadata.json"),
        };

        var result = Assert.Single(_matcher.Match(entries));
        Assert.True(result.IsMatched);
        Assert.Equal(MatchKind.Exact, result.Kind);
    }

    [Fact]
    public void Match_TruncatedSidecar_Issue353()
    {
        var entries = new[]
        {
            Entry("a/IMG_1234.jpg"),
            Entry("a/IMG_1234.jpg.supplem.json"),
        };

        var result = Assert.Single(_matcher.Match(entries));
        Assert.True(result.IsMatched);
    }

    [Fact]
    public void Match_CrossFolder_MediaInAlbumSidecarInYearFolder()
    {
        // The whole point of global indexing: media and sidecar live in different folders.
        var entries = new[]
        {
            Entry("Takeout/Google Photos/Trip to Eilat/IMG_1234.jpg"),
            Entry("Takeout/Google Photos/Photos from 2023/IMG_1234.jpg.supplemental-metadata.json"),
        };

        var media = Assert.Single(_matcher.Match(entries), r => r.Media.FileName == "IMG_1234.jpg");
        Assert.True(media.IsMatched);
    }

    [Fact]
    public void Match_SplitAcrossArchives()
    {
        var entries = new[]
        {
            Entry("a/IMG_1234.jpg", archive: "takeout-001.zip"),
            Entry("a/IMG_1234.jpg.supplemental-metadata.json", archive: "takeout-002.zip"),
        };

        var result = Assert.Single(_matcher.Match(entries));
        Assert.True(result.IsMatched);
    }

    [Fact]
    public void Match_EditedVariant_SharesSidecar()
    {
        var entries = new[]
        {
            Entry("a/IMG_1234.jpg"),
            Entry("a/IMG_1234-edited.jpg"),
            Entry("a/IMG_1234.jpg.supplemental-metadata.json"),
        };

        var results = _matcher.Match(entries);
        Assert.All(results, r => Assert.True(r.IsMatched));
        Assert.Equal(2, results.Count); // two media, one sidecar excluded
    }

    [Fact]
    public void Match_MotionPhotoVideo_InheritsFromSibling()
    {
        // The .MP video half ships without its own JSON and inherits from the .jpg sibling.
        var entries = new[]
        {
            Entry("a/IMG_1234.jpg"),
            Entry("a/IMG_1234.MP"),
            Entry("a/IMG_1234.jpg.supplemental-metadata.json"),
        };

        var results = _matcher.Match(entries);
        var video = Assert.Single(results, r => r.Media.FileName == "IMG_1234.MP");
        Assert.Equal(MatchKind.SiblingInherited, video.Kind);
        Assert.NotNull(video.Sidecar);
    }

    [Fact]
    public void Match_NoSidecar_ReportsUnmatched()
    {
        var entries = new[] { Entry("a/IMG_9999.jpg") };

        var result = Assert.Single(_matcher.Match(entries));
        Assert.False(result.IsMatched);
        Assert.Equal(MatchKind.Unmatched, result.Kind);
    }

    [Fact]
    public void Match_SidecarsAreExcludedFromResults()
    {
        var entries = new[]
        {
            Entry("a/IMG_1234.jpg"),
            Entry("a/IMG_1234.jpg.supplemental-metadata.json"),
        };

        Assert.All(_matcher.Match(entries), r => Assert.False(r.Media.IsSidecar));
    }
}
