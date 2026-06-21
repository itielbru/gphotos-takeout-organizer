using System.Globalization;
using System.Text;
using System.Text.Json;
using GPhotosTakeout.Core.IO;

namespace GPhotosTakeout.Core.Pipeline;

/// <summary>
/// Writes a <see cref="ProcessingReport"/> to disk so the user has a durable record of
/// what happened to every file — useful for verifying a migration and for support.
/// </summary>
public static class ReportExporter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public static async Task WriteJsonAsync(ProcessingReport report, string path, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(report);
        await using var stream = LongPath.Create(path); // creates parent dirs
        await JsonSerializer.SerializeAsync(stream, report, JsonOptions, ct).ConfigureAwait(false);
    }

    public static async Task WriteCsvAsync(ProcessingReport report, string path, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(report);

        var sb = new StringBuilder();
        sb.AppendLine("FileName,SourceFolder,Matched,DateSource,DestinationPath,MetadataWritten,IsDuplicate,Planned,Error");
        foreach (var o in report.Outcomes)
        {
            sb.Append(Csv(o.FileName)).Append(',')
              .Append(Csv(o.SourceFolder)).Append(',')
              .Append(o.Matched).Append(',')
              .Append(Csv(o.DateSource)).Append(',')
              .Append(Csv(o.DestinationPath)).Append(',')
              .Append(o.MetadataWritten).Append(',')
              .Append(o.IsDuplicate).Append(',')
              .Append(o.Planned).Append(',')
              .Append(Csv(o.Error))
              .Append('\n');
        }

        await using var stream = LongPath.Create(path);
        await using var writer = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
        await writer.WriteAsync(sb.ToString().AsMemory(), ct).ConfigureAwait(false);
    }

    private static string Csv(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return "";
        var needsQuote = value.Contains(',', StringComparison.Ordinal)
                      || value.Contains('"', StringComparison.Ordinal)
                      || value.Contains('\n', StringComparison.Ordinal)
                      || value.Contains('\r', StringComparison.Ordinal);
        if (!needsQuote)
            return value;
        return "\"" + value.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
    }
}
