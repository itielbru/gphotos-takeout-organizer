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
    private readonly Lock _gate = new();
    private readonly System.Text.StringBuilder _lostErrors = new();
    private readonly string _exifToolPath;
    private readonly TimeSpan _timeout;

    private ExifToolPool(Channel<ExifToolBatchWriter> available, List<ExifToolBatchWriter> all,
        string exifToolPath, TimeSpan timeout)
    {
        _available = available;
        _all = all;
        _exifToolPath = exifToolPath;
        _timeout = timeout;
    }

    public static ExifToolPool Start(string exifToolPath, int size, TimeSpan? timeout = null)
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
        return new ExifToolPool(channel, all, exifToolPath, timeout ?? TimeSpan.FromMinutes(5));
    }

    public async Task WriteAsync(string filePath, ExifMetadata meta, CancellationToken ct = default)
    {
        var writer = await _available.Reader.ReadAsync(ct).ConfigureAwait(false);

        // A process that died or timed out on a previous file is replaced before reuse.
        if (!writer.IsHealthy)
            writer = await ReplaceAsync(writer).ConfigureAwait(false);

        try
        {
            await writer.WriteAsync(filePath, meta, _timeout, ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Timeout/crash: swap in a fresh process so one bad file doesn't poison
            // the pool, then surface the error to the caller.
            writer = await ReplaceAsync(writer).ConfigureAwait(false);
            throw;
        }
        finally
        {
            await _available.Writer.WriteAsync(writer, CancellationToken.None).ConfigureAwait(false);
        }
    }

    private async Task<ExifToolBatchWriter> ReplaceAsync(ExifToolBatchWriter old)
    {
        var fresh = ExifToolBatchWriter.Start(_exifToolPath);
        lock (_gate)
        {
            // Preserve the dying writer's stderr so it isn't lost on dispose.
            var lost = old.DrainErrors();
            if (!string.IsNullOrWhiteSpace(lost))
                _lostErrors.Append(lost);

            var idx = _all.IndexOf(old);
            if (idx >= 0) _all[idx] = fresh; else _all.Add(fresh);
        }
        await old.DisposeAsync().ConfigureAwait(false);
        return fresh;
    }

    /// <summary>Collects and clears any stderr emitted by the pooled ExifTool processes.</summary>
    public string DrainErrors()
    {
        var sb = new System.Text.StringBuilder();
        lock (_gate)
        {
            sb.Append(_lostErrors);
            _lostErrors.Clear();
            foreach (var writer in _all)
            {
                var errors = writer.DrainErrors();
                if (!string.IsNullOrWhiteSpace(errors))
                    sb.Append(errors);
            }
        }
        return sb.ToString();
    }

    public async ValueTask DisposeAsync()
    {
        _available.Writer.TryComplete();
        List<ExifToolBatchWriter> snapshot;
        lock (_gate) snapshot = new List<ExifToolBatchWriter>(_all);
        foreach (var writer in snapshot)
            await writer.DisposeAsync().ConfigureAwait(false);
    }
}
