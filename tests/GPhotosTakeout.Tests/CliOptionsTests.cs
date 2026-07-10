using GPhotosTakeout.Cli;
using GPhotosTakeout.Core.Models;
using Xunit;

namespace GPhotosTakeout.Tests;

public class CliOptionsTests
{
    // ── Defaults ──────────────────────────────────────────────────────────────

    [Fact]
    public void Parse_NoArgs_HasDocumentedDefaults()
    {
        var o = CliOptions.Parse([]);

        Assert.Empty(o.Inputs);
        Assert.Null(o.Output);
        Assert.Equal(OutputStructure.YearMonth, o.Structure);
        Assert.Equal(AlbumStrategy.Shortcut, o.Albums);
        Assert.Equal(DuplicateHandling.KeepBest, o.Duplicates);
        Assert.True(o.WriteMetadata);
        Assert.True(o.UseExifFallback);
        Assert.False(o.DryRun);
        Assert.False(o.NoLog);
        Assert.False(o.Verbose);
        Assert.False(o.ShowHelp);
        Assert.Null(o.Cpu);
        Assert.Null(o.ExifParallel);
    }

    // ── Inputs & output ───────────────────────────────────────────────────────

    [Fact]
    public void Parse_InputFlag_IsRepeatable()
    {
        var o = CliOptions.Parse(["-i", "a.zip", "--input", "b.zip"]);
        Assert.Equal(new[] { "a.zip", "b.zip" }, o.Inputs);
    }

    [Fact]
    public void Parse_BarePath_TreatedAsInput()
    {
        var o = CliOptions.Parse(["takeout.zip", "-o", "out"]);
        Assert.Equal(new[] { "takeout.zip" }, o.Inputs);
        Assert.Equal("out", o.Output);
    }

    // ── Enums ─────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("yearmonth", OutputStructure.YearMonth)]
    [InlineData("ALBUMS", OutputStructure.Albums)]
    [InlineData("Flat", OutputStructure.Flat)]
    public void Parse_Structure_CaseInsensitive(string value, OutputStructure expected)
    {
        Assert.Equal(expected, CliOptions.Parse(["--structure", value]).Structure);
    }

    [Theory]
    [InlineData("shortcut", AlbumStrategy.Shortcut)]
    [InlineData("duplicate", AlbumStrategy.Duplicate)]
    [InlineData("json", AlbumStrategy.JsonManifest)]      // documented alias
    [InlineData("JSON", AlbumStrategy.JsonManifest)]
    [InlineData("jsonmanifest", AlbumStrategy.JsonManifest)]
    [InlineData("nothing", AlbumStrategy.Nothing)]
    public void Parse_Albums_AcceptsDocumentedValues(string value, AlbumStrategy expected)
    {
        Assert.Equal(expected, CliOptions.Parse(["--albums", value]).Albums);
    }

    [Theory]
    [InlineData("keepbest", DuplicateHandling.KeepBest)]
    [InlineData("keepall", DuplicateHandling.KeepAll)]
    public void Parse_Duplicates(string value, DuplicateHandling expected)
    {
        Assert.Equal(expected, CliOptions.Parse(["--duplicates", value]).Duplicates);
    }

    [Theory]
    [InlineData("--structure", "sideways")]
    [InlineData("--albums", "bogus")]
    [InlineData("--albums", "1")] // bare integers are not documented values
    [InlineData("--duplicates", "2")]
    public void Parse_InvalidEnumValue_Throws(string flag, string value)
    {
        var ex = Assert.Throws<ArgumentException>(() => CliOptions.Parse([flag, value]));
        Assert.Contains(flag, ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Parse_AlbumsError_ListsFriendlyValues()
    {
        var ex = Assert.Throws<ArgumentException>(() => CliOptions.Parse(["--albums", "bogus"]));
        Assert.Contains("json", ex.Message, StringComparison.Ordinal);
    }

    // ── Simple flags ──────────────────────────────────────────────────────────

    [Fact]
    public void Parse_BooleanFlags()
    {
        var o = CliOptions.Parse(["--no-metadata", "--no-exif-fallback", "--dry-run", "--no-log", "-v", "-h"]);
        Assert.False(o.WriteMetadata);
        Assert.False(o.UseExifFallback);
        Assert.True(o.DryRun);
        Assert.True(o.NoLog);
        Assert.True(o.Verbose);
        Assert.True(o.ShowHelp);
    }

    [Fact]
    public void Parse_ValueFlags()
    {
        var o = CliOptions.Parse([
            "--timezone", "Europe/Paris", "--exiftool", @"C:\tools\exiftool.exe",
            "--cpu", "3", "--exif-parallel", "2", "--report", "r.json", "--log", "run.log",
        ]);
        Assert.Equal("Europe/Paris", o.Timezone);
        Assert.Equal(@"C:\tools\exiftool.exe", o.ExifToolPath);
        Assert.Equal(3, o.Cpu);
        Assert.Equal(2, o.ExifParallel);
        Assert.Equal("r.json", o.ReportPath);
        Assert.Equal("run.log", o.LogPath);
    }

    // ── Errors ────────────────────────────────────────────────────────────────

    [Fact]
    public void Parse_UnknownFlag_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() => CliOptions.Parse(["--frobnicate"]));
        Assert.Contains("--frobnicate", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Parse_MissingValue_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() => CliOptions.Parse(["-i", "a.zip", "--output"]));
        Assert.Contains("--output", ex.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("--cpu")]
    [InlineData("--exif-parallel")]
    public void Parse_NonIntegerCount_Throws(string flag)
    {
        Assert.Throws<ArgumentException>(() => CliOptions.Parse([flag, "many"]));
    }

    // ── Mapping to ProcessingOptions ──────────────────────────────────────────

    [Fact]
    public void ToProcessingOptions_MapsAllFields()
    {
        var o = CliOptions.Parse([
            "-i", "a.zip", "-o", "out", "--structure", "flat", "--albums", "json",
            "--duplicates", "keepall", "--timezone", "Europe/Paris",
            "--no-metadata", "--no-exif-fallback", "--cpu", "5", "--exif-parallel", "3", "--dry-run",
        ]);

        var p = o.ToProcessingOptions();

        Assert.Equal(new[] { "a.zip" }, p.InputZipPaths);
        Assert.Equal("out", p.OutputDirectory);
        Assert.Equal(OutputStructure.Flat, p.OutputStructure);
        Assert.Equal(AlbumStrategy.JsonManifest, p.AlbumStrategy);
        Assert.Equal(DuplicateHandling.KeepAll, p.DuplicateHandling);
        Assert.Equal("Europe/Paris", p.FallbackTimeZone);
        Assert.False(p.WriteMetadata);
        Assert.False(p.UseExifFallback);
        Assert.Equal(5, p.CpuParallelism);
        Assert.Equal(3, p.ExifToolParallelism);
        Assert.True(p.DryRun);
    }

    [Fact]
    public void ToProcessingOptions_WhitespaceTimezone_BecomesNull()
    {
        var o = CliOptions.Parse(["-i", "a.zip", "-o", "out", "--timezone", "  "]);
        Assert.Null(o.ToProcessingOptions().FallbackTimeZone);
    }

    [Fact]
    public void ToProcessingOptions_NoOverrides_KeepsProcessingDefaults()
    {
        var defaults = new ProcessingOptions { InputZipPaths = ["a.zip"], OutputDirectory = "out" };
        var p = CliOptions.Parse(["-i", "a.zip", "-o", "out"]).ToProcessingOptions();
        Assert.Equal(defaults.CpuParallelism, p.CpuParallelism);
        Assert.Equal(defaults.ExifToolParallelism, p.ExifToolParallelism);
    }
}
