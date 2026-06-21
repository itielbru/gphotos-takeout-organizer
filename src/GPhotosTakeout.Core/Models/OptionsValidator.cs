namespace GPhotosTakeout.Core.Models;

/// <summary>
/// Outcome of validating a <see cref="ProcessingOptions"/>. <see cref="Errors"/> are
/// blocking (the run must not start); <see cref="Warnings"/> are advisory.
/// </summary>
public sealed record ValidationResult
{
    public IReadOnlyList<string> Errors { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> Warnings { get; init; } = Array.Empty<string>();

    public bool IsValid => Errors.Count == 0;
}

/// <summary>
/// Pre-flight validation for a processing run. Catches misconfiguration (missing input,
/// unwritable output, invalid timezone, bad parallelism) up front with a clear message,
/// instead of failing deep inside the pipeline after work has begun.
/// </summary>
public static class OptionsValidator
{
    public static ValidationResult Validate(ProcessingOptions options, bool exifToolAvailable)
    {
        ArgumentNullException.ThrowIfNull(options);

        var errors = new List<string>();
        var warnings = new List<string>();

        // Input archives.
        if (options.InputZipPaths is null || options.InputZipPaths.Count == 0)
        {
            errors.Add("No input ZIP files were selected.");
        }
        else
        {
            foreach (var zip in options.InputZipPaths)
            {
                if (string.IsNullOrWhiteSpace(zip))
                    errors.Add("An input path is empty.");
                else if (!File.Exists(zip))
                    errors.Add($"Input file not found: {zip}");
            }
        }

        // Output directory.
        if (string.IsNullOrWhiteSpace(options.OutputDirectory))
        {
            errors.Add("No output directory was selected.");
        }
        else if (!IsOutputWritable(options.OutputDirectory, out var reason))
        {
            errors.Add($"Output directory is not writable: {reason}");
        }

        // Parallelism.
        if (options.CpuParallelism < 1)
            errors.Add($"CPU parallelism must be at least 1 (was {options.CpuParallelism}).");
        if (options.ExifToolParallelism < 1)
            errors.Add($"ExifTool parallelism must be at least 1 (was {options.ExifToolParallelism}).");

        // Fallback timezone.
        if (!string.IsNullOrWhiteSpace(options.FallbackTimeZone)
            && !TimeZoneInfo.TryFindSystemTimeZoneById(options.FallbackTimeZone, out _))
        {
            errors.Add($"Fallback timezone is not a valid IANA id: '{options.FallbackTimeZone}'.");
        }

        // Metadata writing.
        if (options.WriteMetadata && !exifToolAvailable)
        {
            warnings.Add("ExifTool was not found — files will be organized and dated, "
                       + "but EXIF/XMP metadata will not be written into them.");
        }

        return new ValidationResult { Errors = errors, Warnings = warnings };
    }

    private static bool IsOutputWritable(string directory, out string reason)
    {
        try
        {
            Directory.CreateDirectory(directory);
            var probe = Path.Combine(directory, ".gphotos-write-probe-" + Guid.NewGuid().ToString("N"));
            File.WriteAllText(probe, "");
            File.Delete(probe);
            reason = "";
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            reason = ex.Message;
            return false;
        }
    }
}
