using System.IO.Compression;
using System.Text;
using System.Text.Json;
using GPhotosTakeout.Core.Models;
using GPhotosTakeout.Core.Pipeline;
using Xunit;

namespace GPhotosTakeout.Tests;

public class OptionsValidatorTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "gphval_" + Guid.NewGuid().ToString("N"));

    public OptionsValidatorTests() => Directory.CreateDirectory(_dir);

    private string MakeZip()
    {
        var zip = Path.Combine(_dir, "in.zip");
        using var z = ZipFile.Open(zip, ZipArchiveMode.Create);
        z.CreateEntry("x.txt");
        return zip;
    }

    [Fact]
    public void Valid_Options_Pass()
    {
        var options = new ProcessingOptions
        {
            InputZipPaths = new[] { MakeZip() },
            OutputDirectory = Path.Combine(_dir, "out"),
            FallbackTimeZone = "Asia/Jerusalem",
        };

        var result = OptionsValidator.Validate(options, exifToolAvailable: true);

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public void Missing_Input_And_Output_Are_Errors()
    {
        var options = new ProcessingOptions
        {
            InputZipPaths = Array.Empty<string>(),
            OutputDirectory = "",
        };

        var result = OptionsValidator.Validate(options, exifToolAvailable: true);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("input", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result.Errors, e => e.Contains("output", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void NonExistent_Input_File_Is_Error()
    {
        var options = new ProcessingOptions
        {
            InputZipPaths = new[] { Path.Combine(_dir, "nope.zip") },
            OutputDirectory = Path.Combine(_dir, "out"),
        };

        var result = OptionsValidator.Validate(options, exifToolAvailable: true);
        Assert.Contains(result.Errors, e => e.Contains("not found", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Invalid_Timezone_Is_Error()
    {
        var options = new ProcessingOptions
        {
            InputZipPaths = new[] { MakeZip() },
            OutputDirectory = Path.Combine(_dir, "out"),
            FallbackTimeZone = "Not/AZone",
        };

        var result = OptionsValidator.Validate(options, exifToolAvailable: true);
        Assert.Contains(result.Errors, e => e.Contains("timezone", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Bad_Parallelism_Is_Error()
    {
        var options = new ProcessingOptions
        {
            InputZipPaths = new[] { MakeZip() },
            OutputDirectory = Path.Combine(_dir, "out"),
            CpuParallelism = 0,
            ExifToolParallelism = 0,
        };

        var result = OptionsValidator.Validate(options, exifToolAvailable: true);
        Assert.Equal(2, result.Errors.Count(e => e.Contains("parallelism", StringComparison.OrdinalIgnoreCase)));
    }

    [Fact]
    public void Missing_ExifTool_Is_Warning_Not_Error()
    {
        var options = new ProcessingOptions
        {
            InputZipPaths = new[] { MakeZip() },
            OutputDirectory = Path.Combine(_dir, "out"),
            WriteMetadata = true,
        };

        var result = OptionsValidator.Validate(options, exifToolAvailable: false);
        Assert.True(result.IsValid);
        Assert.Contains(result.Warnings, w => w.Contains("ExifTool", StringComparison.OrdinalIgnoreCase));
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { /* ignore */ }
    }
}

public class DryRunTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "gphdry_" + Guid.NewGuid().ToString("N"));

    public DryRunTests() => Directory.CreateDirectory(_dir);

    [Fact]
    public async Task DryRun_PlansButWritesNothing()
    {
        var zipPath = Path.Combine(_dir, "takeout-001.zip");
        using (var zip = ZipFile.Open(zipPath, ZipArchiveMode.Create))
        {
            var entry = zip.CreateEntry("Takeout/Google Photos/Photos from 2023/IMG_1234.jpg");
            using (var s = entry.Open()) s.Write(new byte[] { 1, 2, 3, 4 });
            var j = zip.CreateEntry("Takeout/Google Photos/Photos from 2023/IMG_1234.jpg.supplemental-metadata.json");
            using (var s = j.Open()) s.Write(Encoding.UTF8.GetBytes(
                "{\"photoTakenTime\":{\"timestamp\":\"1692108336\"}}"));
        }

        var output = Path.Combine(_dir, "out");
        var options = new ProcessingOptions
        {
            InputZipPaths = new[] { zipPath },
            OutputDirectory = output,
            WriteMetadata = false,
            CpuParallelism = 1,
            DryRun = true,
        };

        var report = await new ProcessingPipeline(null).RunAsync(options);

        Assert.True(report.DryRun);
        Assert.Equal(1, report.TotalMedia);
        Assert.All(report.Outcomes, o => Assert.True(o.Planned));
        Assert.Contains(report.Outcomes, o => o.DestinationPath!.Contains("2023-08", StringComparison.Ordinal));

        // Nothing was actually written to the output tree.
        var media = Directory.Exists(output)
            ? Directory.EnumerateFiles(output, "*.jpg", SearchOption.AllDirectories)
            : Enumerable.Empty<string>();
        Assert.Empty(media);
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { /* ignore */ }
    }
}

public class ReportExporterTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "gphrep_" + Guid.NewGuid().ToString("N"));

    public ReportExporterTests() => Directory.CreateDirectory(_dir);

    private static ProcessingReport SampleReport() => new()
    {
        TotalMedia = 2,
        Matched = 1,
        Errors = 1,
        ErrorMessages = new[] { "boom" },
        Outcomes = new[]
        {
            new FileOutcome { FileName = "a,b.jpg", SourceFolder = "Album \"X\"", Matched = true,
                DateSource = "Json", DestinationPath = @"C:\out\2023\a,b.jpg", MetadataWritten = true },
            new FileOutcome { FileName = "c.jpg", SourceFolder = "Photos from 2023", Matched = false,
                Error = "line1\nline2" },
        },
    };

    [Fact]
    public async Task Json_RoundTrips()
    {
        var path = Path.Combine(_dir, "report.json");
        await ReportExporter.WriteJsonAsync(SampleReport(), path);

        var text = await File.ReadAllTextAsync(path);
        var back = JsonSerializer.Deserialize<ProcessingReport>(text);

        Assert.NotNull(back);
        Assert.Equal(2, back!.TotalMedia);
        Assert.Equal(2, back.Outcomes.Count);
        Assert.Equal("a,b.jpg", back.Outcomes[0].FileName);
    }

    [Fact]
    public async Task Csv_QuotesFieldsWithCommasAndNewlines()
    {
        var path = Path.Combine(_dir, "report.csv");
        await ReportExporter.WriteCsvAsync(SampleReport(), path);

        var lines = await File.ReadAllLinesAsync(path);
        Assert.Equal("FileName,SourceFolder,Matched,DateSource,DestinationPath,MetadataWritten,IsDuplicate,Planned,Error",
            lines[0]);
        // Field with a comma must be quoted.
        Assert.Contains("\"a,b.jpg\"", lines[1]);
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { /* ignore */ }
    }
}

public class ProgressEtaTests
{
    [Fact]
    public void Eta_And_Throughput_Compute()
    {
        var p = new ProcessingProgress { Phase = "Processing", Total = 100, Processed = 25, ElapsedSeconds = 5 };
        Assert.Equal(5.0, p.ItemsPerSecond, 3);   // 25 / 5s
        Assert.Equal(15.0, p.EtaSeconds!.Value, 3); // 75 remaining / 5 per s
        Assert.Equal(0.25, p.Fraction, 3);
    }

    [Fact]
    public void Eta_Null_BeforeAnyProgress()
    {
        var p = new ProcessingProgress { Phase = "Indexing" };
        Assert.Null(p.EtaSeconds);
        Assert.Equal(0, p.ItemsPerSecond);
    }
}
