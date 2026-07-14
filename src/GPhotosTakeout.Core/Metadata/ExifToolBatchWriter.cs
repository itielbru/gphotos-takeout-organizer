using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Threading.Channels;
using GPhotosTakeout.Core.Models;

namespace GPhotosTakeout.Core.Metadata;

/// <summary>
/// Writes metadata into media files via a single long-lived ExifTool process in
/// <c>-stay_open</c> batch mode. ExifTool returns no exit code per command; it
/// prints a sentinel line after each <c>-execute</c>, so we read stdout
/// asynchronously and complete one waiter per command.
///
/// The process is fed argument files (one argument per line) over stdin. Tags are
/// chosen per format: still images get EXIF DateTimeOriginal + OffsetTimeOriginal,
/// videos get QuickTime CreateDate (UTC). Mandatory flags: UTF-8 filename charset
/// (Hebrew names), overwrite-original (no _original copies), LargeFileSupport
/// (big videos).
/// </summary>
public sealed class ExifToolBatchWriter : IAsyncDisposable
{
    private const string Sentinel = "{ready}";
    private const int MaxErrorBufferChars = 256 * 1024;
    private static readonly HashSet<string> VideoExtensions =
        new(StringComparer.OrdinalIgnoreCase) { ".mp4", ".mov", ".m4v", ".mp", ".mv", ".3gp" };

    private readonly Process _process;
    private readonly Channel<TaskCompletionSource<bool>> _waiters =
        Channel.CreateUnbounded<TaskCompletionSource<bool>>();
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private readonly StringBuilder _errorBuffer = new();
    private volatile bool _faulted;
    private volatile bool _disposing;

    private ExifToolBatchWriter(Process process)
    {
        _process = process;
        _ = ReadStdOutLoopAsync();
        _ = ReadStdErrLoopAsync();
    }

    /// <summary>False once the process has died or a command has timed out; the pool
    /// then discards this writer and starts a fresh one.</summary>
    public bool IsHealthy => !_faulted && !_process.HasExited;

    public static ExifToolBatchWriter Start(string exifToolPath)
    {
        var psi = new ProcessStartInfo
        {
            FileName = exifToolPath,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
            // Must be explicit UTF-8 (no BOM): the default stdin encoding follows the
            // console code page — and a GUI app has no console, so it falls back to
            // the ANSI/OEM page, mangling non-ASCII (Hebrew) filenames into bytes that
            // ExifTool rejects with "Invalid filename encoding" and skips the write.
            // Must match the "-charset filename=utf8" argument sent per command.
            StandardInputEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
        };
        // -stay_open keeps the process alive; -@ - reads arg-files from stdin.
        psi.ArgumentList.Add("-stay_open");
        psi.ArgumentList.Add("True");
        psi.ArgumentList.Add("-@");
        psi.ArgumentList.Add("-");

        var process = new Process { StartInfo = psi };
        if (!process.Start())
            throw new InvalidOperationException("Failed to start ExifTool.");

        return new ExifToolBatchWriter(process);
    }

    /// <summary>Writes the given metadata into one already-extracted media file.</summary>
    /// <param name="timeout">Max time to wait for ExifTool to acknowledge the write
    /// before the process is treated as hung and faulted.</param>
    public async Task WriteAsync(string filePath, ExifMetadata meta, TimeSpan timeout, CancellationToken ct = default)
    {
        if (meta.IsEmpty)
            return;

        if (_faulted || _process.HasExited)
            throw new InvalidOperationException("ExifTool process is not available (faulted or exited).");

        var args = BuildArgs(filePath, meta);
        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        await _writeLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            await _waiters.Writer.WriteAsync(tcs, ct).ConfigureAwait(false);
            foreach (var arg in args)
                await _process.StandardInput.WriteLineAsync(arg.AsMemory(), ct).ConfigureAwait(false);
            await _process.StandardInput.WriteLineAsync("-execute".AsMemory(), ct).ConfigureAwait(false);
            await _process.StandardInput.FlushAsync(ct).ConfigureAwait(false);
        }
        finally
        {
            _writeLock.Release();
        }

        try
        {
            await tcs.Task.WaitAsync(timeout, ct).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            // The single -stay_open process no longer aligns request↔response, so we
            // must tear it down; the pool replaces it with a fresh process.
            Fault(new TimeoutException($"ExifTool timed out after {timeout.TotalSeconds:0}s on this file."));
            throw new TimeoutException(
                $"ExifTool timed out writing '{Path.GetFileName(filePath)}' after {timeout.TotalSeconds:0}s.");
        }
    }

    private void Fault(Exception ex)
    {
        _faulted = true;
        FailAllWaiters(ex);
        try { if (!_process.HasExited) _process.Kill(entireProcessTree: true); }
        catch { /* already gone */ }
    }

    /// <summary>Builds the per-file ExifTool argument list (one token per line).</summary>
    internal static IReadOnlyList<string> BuildArgs(string filePath, ExifMetadata meta)
    {
        var args = new List<string>
        {
            "-charset", "filename=utf8",
            "-codedcharacterset=utf8",
            "-overwrite_original",
            "-api", "LargeFileSupport=1",
        };

        var isVideo = VideoExtensions.Contains(Path.GetExtension(filePath));

        if (isVideo)
        {
            args.Add("-api");
            args.Add("QuickTimeUTC=1");
            if (meta.DateTakenUtc is { } utc)
            {
                var v = utc.ToString("yyyy:MM:dd HH:mm:ss", CultureInfo.InvariantCulture);
                args.Add($"-QuickTime:CreateDate={v}");
                args.Add($"-QuickTime:ModifyDate={v}");
            }
            if (meta.HasGps)
                args.Add($"-Keys:GPSCoordinates={FormatIso6709(meta)}");
        }
        else
        {
            if (meta.DateTakenLocal is { } local)
            {
                var v = local.ToString("yyyy:MM:dd HH:mm:ss", CultureInfo.InvariantCulture);
                args.Add($"-EXIF:DateTimeOriginal={v}");
                args.Add($"-EXIF:CreateDate={v}");
                if (!string.IsNullOrEmpty(meta.Offset))
                {
                    args.Add($"-EXIF:OffsetTimeOriginal={meta.Offset}");
                    args.Add($"-EXIF:OffsetTimeDigitized={meta.Offset}");
                }
            }
            if (meta.HasGps)
            {
                args.Add($"-EXIF:GPSLatitude={Math.Abs(meta.Latitude!.Value).ToString(CultureInfo.InvariantCulture)}");
                args.Add($"-EXIF:GPSLatitudeRef={(meta.Latitude >= 0 ? "N" : "S")}");
                args.Add($"-EXIF:GPSLongitude={Math.Abs(meta.Longitude!.Value).ToString(CultureInfo.InvariantCulture)}");
                args.Add($"-EXIF:GPSLongitudeRef={(meta.Longitude >= 0 ? "E" : "W")}");
                if (meta.Altitude is { } alt)
                    args.Add($"-EXIF:GPSAltitude={alt.ToString(CultureInfo.InvariantCulture)}");
            }
        }

        if (!string.IsNullOrEmpty(meta.Description))
        {
            args.Add($"-XMP-dc:Description={meta.Description}");
            if (!isVideo)
                args.Add($"-EXIF:ImageDescription={meta.Description}");
        }

        if (meta.Favorited)
            args.Add("-XMP:Rating=5");

        args.Add(filePath);
        return args;
    }

    private static string FormatIso6709(ExifMetadata meta) =>
        string.Format(CultureInfo.InvariantCulture, "{0:+00.0000;-00.0000}{1:+000.0000;-000.0000}/",
            meta.Latitude!.Value, meta.Longitude!.Value);

    private async Task ReadStdOutLoopAsync()
    {
        try
        {
            string? line;
            while ((line = await _process.StandardOutput.ReadLineAsync().ConfigureAwait(false)) is not null)
            {
                if (line.StartsWith(Sentinel, StringComparison.Ordinal) &&
                    _waiters.Reader.TryRead(out var waiter))
                {
                    waiter.TrySetResult(true);
                }
            }

            // stdout closed: the process exited. If that wasn't us shutting it down,
            // any in-flight waiter would hang forever — fault them instead.
            if (!_disposing)
                Fault(new IOException("ExifTool exited unexpectedly."));
        }
        catch (Exception ex)
        {
            Fault(ex);
        }
    }

    private async Task ReadStdErrLoopAsync()
    {
        try
        {
            string? line;
            while ((line = await _process.StandardError.ReadLineAsync().ConfigureAwait(false)) is not null)
            {
                lock (_errorBuffer)
                {
                    _errorBuffer.AppendLine(line);
                    if (_errorBuffer.Length > MaxErrorBufferChars)
                        _errorBuffer.Remove(0, _errorBuffer.Length - MaxErrorBufferChars);
                }
            }
        }
        catch
        {
            // process tearing down; ignore.
        }
    }

    public string DrainErrors()
    {
        lock (_errorBuffer)
        {
            var s = _errorBuffer.ToString();
            _errorBuffer.Clear();
            return s;
        }
    }

    private void FailAllWaiters(Exception ex)
    {
        while (_waiters.Reader.TryRead(out var waiter))
            waiter.TrySetException(ex);
    }

    public async ValueTask DisposeAsync()
    {
        _disposing = true;
        try
        {
            await _process.StandardInput.WriteLineAsync("-stay_open").ConfigureAwait(false);
            await _process.StandardInput.WriteLineAsync("False").ConfigureAwait(false);
            await _process.StandardInput.FlushAsync().ConfigureAwait(false);
            if (!_process.WaitForExit(5000))
                _process.Kill(entireProcessTree: true);
        }
        catch
        {
            try { _process.Kill(entireProcessTree: true); } catch { /* already gone */ }
        }
        finally
        {
            _process.Dispose();
            _writeLock.Dispose();
        }
    }
}
