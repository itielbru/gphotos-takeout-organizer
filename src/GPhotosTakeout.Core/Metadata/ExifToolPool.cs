using System.Threading.Channels;

namespace GPhotosTakeout.Core.Metadata;

/// <summary>
/// A small fixed pool of long-lived ExifTool processes. Metadata writing is
/// I/O-bound, so the pool is intentionally tiny (single digits) to avoid disk
/// thrashing. Borrow/return is modeled with a bounded channel.
/// </summary>
public sealed class ExifToolPool : IAsyncDisposable
{
    private readonly Channel<ExifToolBatchWriter> _available;
    private readonly List<ExifToolBatchWriter> _all;

    private ExifToolPool(Channel<ExifToolBatchWriter> available, List<ExifToolBatchWriter> all)
    {
        _available = available;
        _all = all;
    }

    public static ExifToolPool Start(string exifToolPath, int size)
    {
        size = Math.Max(1, size);
        var channel = Channel.CreateBounded<ExifToolBatchWriter>(size);
        var all = new List<ExifToolBatchWriter>(size);
        for (var i = 0; i < size; i++)
        {
            var writer = ExifToolBatchWriter.Start(exifToolPath);
            all.Add(writer);
            channel.Writer.TryWrite(writer);
        }
        return new ExifToolPool(channel, all);
    }

    public async Task WriteAsync(string filePath, ExifMetadata meta, CancellationToken ct = default)
    {
        var writer = await _available.Reader.ReadAsync(ct).ConfigureAwait(false);
        try
        {
            await writer.WriteAsync(filePath, meta, ct).ConfigureAwait(false);
        }
        finally
        {
            await _available.Writer.WriteAsync(writer, CancellationToken.None).ConfigureAwait(false);
        }
    }

    /// <summary>Collects and clears any stderr emitted by the pooled ExifTool processes.</summary>
    public string DrainErrors()
    {
        var sb = new System.Text.StringBuilder();
        foreach (var writer in _all)
        {
            var errors = writer.DrainErrors();
            if (!string.IsNullOrWhiteSpace(errors))
                sb.Append(errors);
        }
        return sb.ToString();
    }

    public async ValueTask DisposeAsync()
    {
        _available.Writer.TryComplete();
        foreach (var writer in _all)
            await writer.DisposeAsync().ConfigureAwait(false);
    }
}
