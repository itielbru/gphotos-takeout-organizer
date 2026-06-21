using GPhotosTakeout.Core.Models;

namespace GPhotosTakeout.Cli;

/// <summary>Parses argv into a <see cref="ProcessingOptions"/> plus CLI-only flags.</summary>
internal sealed class CliOptions
{
    public List<string> Inputs { get; } = new();
    public string? Output { get; private set; }
    public OutputStructure Structure { get; private set; } = OutputStructure.YearMonth;
    public AlbumStrategy Albums { get; private set; } = AlbumStrategy.Shortcut;
    public DuplicateHandling Duplicates { get; private set; } = DuplicateHandling.KeepBest;
    public string? Timezone { get; private set; } = "Asia/Jerusalem";
    public bool WriteMetadata { get; private set; } = true;
    public string? ExifToolPath { get; private set; }
    public int? Cpu { get; private set; }
    public int? ExifParallel { get; private set; }
    public bool DryRun { get; private set; }
    public string? ReportPath { get; private set; }
    public bool ShowHelp { get; private set; }

    public ProcessingOptions ToProcessingOptions()
    {
        var opt = new ProcessingOptions
        {
            InputZipPaths = Inputs,
            OutputDirectory = Output ?? "",
            OutputStructure = Structure,
            AlbumStrategy = Albums,
            DuplicateHandling = Duplicates,
            FallbackTimeZone = string.IsNullOrWhiteSpace(Timezone) ? null : Timezone,
            WriteMetadata = WriteMetadata,
            DryRun = DryRun,
        };
        if (Cpu is { } cpu) opt = opt with { CpuParallelism = cpu };
        if (ExifParallel is { } ep) opt = opt with { ExifToolParallelism = ep };
        return opt;
    }

    /// <summary>Parses args. Throws <see cref="ArgumentException"/> on a usage error.</summary>
    public static CliOptions Parse(string[] args)
    {
        var o = new CliOptions();
        for (var i = 0; i < args.Length; i++)
        {
            var a = args[i];
            switch (a)
            {
                case "-h" or "--help": o.ShowHelp = true; break;
                case "-i" or "--input": o.Inputs.Add(Next(args, ref i, a)); break;
                case "-o" or "--output": o.Output = Next(args, ref i, a); break;
                case "--structure": o.Structure = ParseEnum<OutputStructure>(Next(args, ref i, a), a); break;
                case "--albums": o.Albums = ParseEnum<AlbumStrategy>(Next(args, ref i, a), a); break;
                case "--duplicates": o.Duplicates = ParseEnum<DuplicateHandling>(Next(args, ref i, a), a); break;
                case "--timezone": o.Timezone = Next(args, ref i, a); break;
                case "--no-metadata": o.WriteMetadata = false; break;
                case "--exiftool": o.ExifToolPath = Next(args, ref i, a); break;
                case "--cpu": o.Cpu = ParseInt(Next(args, ref i, a), a); break;
                case "--exif-parallel": o.ExifParallel = ParseInt(Next(args, ref i, a), a); break;
                case "--dry-run": o.DryRun = true; break;
                case "--report": o.ReportPath = Next(args, ref i, a); break;
                default:
                    // Bare path → treat as an input zip for convenience.
                    if (!a.StartsWith('-')) o.Inputs.Add(a);
                    else throw new ArgumentException($"Unknown option: {a}");
                    break;
            }
        }
        return o;
    }

    private static string Next(string[] args, ref int i, string flag)
    {
        if (i + 1 >= args.Length)
            throw new ArgumentException($"Option {flag} requires a value.");
        return args[++i];
    }

    private static int ParseInt(string value, string flag) =>
        int.TryParse(value, out var n)
            ? n
            : throw new ArgumentException($"Option {flag} expects an integer, got '{value}'.");

    private static T ParseEnum<T>(string value, string flag) where T : struct, Enum =>
        Enum.TryParse<T>(value, ignoreCase: true, out var v)
            ? v
            : throw new ArgumentException(
                $"Option {flag} expects one of [{string.Join(", ", Enum.GetNames<T>())}], got '{value}'.");
}
