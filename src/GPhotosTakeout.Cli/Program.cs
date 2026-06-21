using System.Globalization;
using GPhotosTakeout.Cli;
using GPhotosTakeout.Core.Logging;
using GPhotosTakeout.Core.Models;
using GPhotosTakeout.Core.Pipeline;
using Microsoft.Extensions.Logging;

// Exit codes: 0 success · 1 completed with errors · 2 invalid options · 3 cancelled · 64 usage error.
const int ExitOk = 0, ExitErrors = 1, ExitInvalid = 2, ExitCancelled = 3, ExitUsage = 64;

CliOptions cli;
try
{
    cli = CliOptions.Parse(args);
}
catch (ArgumentException ex)
{
    Console.Error.WriteLine("Error: " + ex.Message);
    Console.Error.WriteLine();
    PrintUsage();
    return ExitUsage;
}

if (cli.ShowHelp || args.Length == 0)
{
    PrintUsage();
    return cli.ShowHelp ? ExitOk : ExitUsage;
}

var exifToolPath = cli.WriteMetadata ? (cli.ExifToolPath ?? LocateExifTool()) : null;

var options = cli.ToProcessingOptions();
var validation = OptionsValidator.Validate(options, exifToolAvailable: exifToolPath is not null);
foreach (var w in validation.Warnings)
    Console.Error.WriteLine("Warning: " + w);
if (!validation.IsValid)
{
    foreach (var e in validation.Errors)
        Console.Error.WriteLine("Error: " + e);
    return ExitInvalid;
}

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true; // let the pipeline unwind gracefully instead of hard-killing
    Console.Error.WriteLine();
    Console.Error.WriteLine("Cancelling… (finishing in-flight files)");
    cts.Cancel();
};

var lastLineLength = 0;
var progress = new Progress<ProcessingProgress>(p =>
{
    var eta = p.EtaSeconds is { } s ? TimeSpan.FromSeconds(s).ToString(@"hh\:mm\:ss", CultureInfo.InvariantCulture) : "--:--:--";
    var line = p.Phase == "Processing"
        ? $"\r{p.Phase}: {p.Processed}/{p.Total} ({p.Fraction:P0})  {p.ItemsPerSecond:F1}/s  ETA {eta}  err {p.Errors}"
        : $"\r{p.Phase}…";
    Console.Write(line.PadRight(Math.Max(lastLineLength, line.Length)));
    lastLineLength = line.Length;
});

Console.WriteLine($"GPhotos Takeout organizer (CLI){(options.DryRun ? "  [DRY RUN]" : "")}");
Console.WriteLine($"  input(s): {options.InputZipPaths.Count}   output: {options.OutputDirectory}");
Console.WriteLine($"  metadata: {(exifToolPath is not null ? "on (" + exifToolPath + ")" : "off")}");
Console.WriteLine();

FileLoggerProvider? logProvider = null;
ILogger? logger = null;
if (!cli.NoLog)
{
    var logPath = cli.LogPath ?? Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "GPhotosTakeout", "logs", $"cli-{DateTime.Now:yyyyMMdd-HHmmss}.log");
    logProvider = new FileLoggerProvider(logPath, cli.Verbose ? LogLevel.Debug : LogLevel.Information);
    logger = logProvider.CreateLogger("Pipeline");
    Console.WriteLine("  log: " + logPath);
}

ProcessingReport report;
try
{
    report = await new ProcessingPipeline(exifToolPath, logger).RunAsync(options, progress, cts.Token);
}
catch (OperationCanceledException)
{
    Console.WriteLine();
    Console.Error.WriteLine("Cancelled.");
    return ExitCancelled;
}
catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
{
    Console.WriteLine();
    Console.Error.WriteLine("Fatal: " + ex.Message);
    return ExitErrors;
}

Console.WriteLine();
Console.WriteLine();
Console.WriteLine(options.DryRun ? "Dry-run plan:" : "Done:");
Console.WriteLine($"  total media       : {report.TotalMedia}");
Console.WriteLine($"  matched to JSON   : {report.Matched}");
Console.WriteLine($"  unmatched         : {report.Unmatched}");
Console.WriteLine($"  duplicates        : {report.Duplicates}");
Console.WriteLine($"  metadata written  : {report.MetadataWritten}");
Console.WriteLine($"  special folders   : {report.SpecialFolderItems}");
Console.WriteLine($"  errors            : {report.Errors}");
if (report.Cancelled)
    Console.WriteLine("  (run was cancelled — partial result)");

if (report.Errors > 0)
{
    Console.WriteLine();
    Console.WriteLine("First errors:");
    foreach (var msg in report.ErrorMessages.Take(10))
        Console.WriteLine("  - " + msg);
}

if (cli.ReportPath is { } reportPath)
{
    if (reportPath.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
        await ReportExporter.WriteCsvAsync(report, reportPath, cts.Token);
    else
        await ReportExporter.WriteJsonAsync(report, reportPath, cts.Token);
    Console.WriteLine();
    Console.WriteLine("Report written to: " + reportPath);
}

logProvider?.Dispose();
return report.Cancelled ? ExitCancelled : report.Errors > 0 ? ExitErrors : ExitOk;

static string? LocateExifTool()
{
    var baseDir = AppContext.BaseDirectory;
    string[] candidates =
    [
        Path.Combine(baseDir, "Tools", "exiftool.exe"),
        Path.Combine(baseDir, "exiftool.exe"),
    ];
    return candidates.FirstOrDefault(File.Exists);
}

static void PrintUsage()
{
    Console.WriteLine(
"""
gptakeout — organize a Google Photos Takeout into a clean, dated library.

USAGE:
  gptakeout -i <takeout.zip> [-i <more.zip> ...] -o <output-dir> [options]
  gptakeout <takeout.zip> -o <output-dir>            (bare paths are treated as inputs)

OPTIONS:
  -i, --input <zip>        Input Takeout ZIP (repeatable).
  -o, --output <dir>       Output directory.
      --structure <s>      yearmonth | albums | flat        (default: yearmonth)
      --albums <s>         shortcut | duplicate | json | nothing  (default: shortcut)
      --duplicates <s>     keepbest | keepall                (default: keepbest)
      --timezone <iana>    Fallback timezone for photos without GPS (default: Asia/Jerusalem).
      --no-metadata        Do not write EXIF/XMP (skip ExifTool).
      --exiftool <path>    Path to exiftool(.exe). Auto-detected from ./Tools otherwise.
      --cpu <n>            CPU-bound parallelism.
      --exif-parallel <n>  Concurrent ExifTool processes.
      --dry-run            Plan and report only; write nothing.
      --report <path>      Write a per-file report (.json or .csv).
      --log <path>         Write the run log here (default: %LocalAppData%\GPhotosTakeout\logs).
      --no-log             Do not write a log file.
  -v, --verbose            Verbose (Debug-level) logging.
  -h, --help               Show this help.
""");
}
