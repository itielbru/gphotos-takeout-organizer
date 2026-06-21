using System.Collections.Concurrent;
using GPhotosTakeout.Core.Albums;
using GPhotosTakeout.Core.Archives;
using GPhotosTakeout.Core.Dates;
using GPhotosTakeout.Core.Dedup;
using GPhotosTakeout.Core.IO;
using GPhotosTakeout.Core.Matching;
using GPhotosTakeout.Core.Metadata;
using GPhotosTakeout.Core.Models;

namespace GPhotosTakeout.Core.Pipeline;

/// <summary>
/// Orchestrates a full run: index archives → match sidecars → for each media file
/// resolve its date/timezone, extract it to the organized output tree, de-duplicate,
/// embed metadata via ExifTool, and link album copies. Reports progress and supports
/// cancellation and resume.
/// </summary>
public sealed class ProcessingPipeline
{
    private readonly string? _exifToolPath;

    /// <param name="exifToolPath">Path to exiftool(.exe). If null, metadata writing is skipped.</param>
    public ProcessingPipeline(string? exifToolPath) => _exifToolPath = exifToolPath;

    public async Task<ProcessingReport> RunAsync(
        ProcessingOptions options,
        IProgress<ProcessingProgress>? progress = null,
        CancellationToken ct = default)
    {
        using var reader = new TakeoutArchiveReader();
        progress?.Report(new ProcessingProgress { Phase = "Indexing" });
        var entries = reader.Index(options.InputZipPaths);

        progress?.Report(new ProcessingProgress { Phase = "Matching", Total = entries.Count });
        var matches = new SidecarMatcher().Match(entries);
        var media = matches.Where(m => !m.Media.IsSidecar).ToList();

        using var journal = ResumeJournal.Open(options.OutputDirectory);
        var tz = new TimezoneResolver(options.FallbackTimeZone);
        var dateResolver = new DateResolver();
        var dedup = new HashDeduplicator();
        var linker = new AlbumLinker();
        var pathBuilder = new OutputPathBuilder(options.OutputStructure);

        await using var exifPool = _exifToolPath is not null && options.WriteMetadata && !options.DryRun
            ? ExifToolPool.Start(_exifToolPath, options.ExifToolParallelism)
            : null;

        var counters = new Counters();
        var errors = new ConcurrentBag<string>();
        var outcomes = new ConcurrentBag<FileOutcome>();
        var processed = 0;
        var clock = System.Diagnostics.Stopwatch.StartNew();

        var parallelism = options.OutputStructure == OutputStructure.YearMonth
            ? options.CpuParallelism
            : 1; // album/flat dest dirs are shared; keep simple ordering

        try
        {
            await Parallel.ForEachAsync(
                media,
                new ParallelOptions { MaxDegreeOfParallelism = parallelism, CancellationToken = ct },
                async (match, token) =>
                {
                    var key = match.Media.ArchiveId + "|" + match.Media.Path;
                    try
                    {
                        if (!options.DryRun && journal.IsDone(key))
                            return;

                        if (match.IsMatched) Interlocked.Increment(ref counters.Matched);
                        else Interlocked.Increment(ref counters.Unmatched);

                        var outcome = await ProcessOneAsync(match, options, reader, dateResolver, tz, pathBuilder,
                            dedup, linker, exifPool, counters, options.DryRun, token).ConfigureAwait(false);
                        outcomes.Add(outcome);

                        if (!options.DryRun) journal.MarkDone(key);
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        Interlocked.Increment(ref counters.Errors);
                        errors.Add($"{match.Media.FileName}: {ex.Message}");
                        outcomes.Add(new FileOutcome
                        {
                            FileName = match.Media.FileName,
                            SourceFolder = match.Media.Folder,
                            Matched = match.IsMatched,
                            Error = ex.Message,
                        });
                    }
                    finally
                    {
                        var done = Interlocked.Increment(ref processed);
                        progress?.Report(new ProcessingProgress
                        {
                            Phase = "Processing",
                            Total = media.Count,
                            Processed = done,
                            CurrentFile = match.Media.FileName,
                            Errors = counters.Errors,
                            ElapsedSeconds = clock.Elapsed.TotalSeconds,
                        });
                    }
                }).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Expected when the user cancels: fall through and return a partial
            // report (already-done work is persisted in the journal for resume).
        }

        // Surface any ExifTool stderr that accumulated during the run; otherwise
        // metadata-write failures would be completely invisible to the user.
        if (exifPool is not null)
        {
            var exifErrors = exifPool.DrainErrors();
            if (!string.IsNullOrWhiteSpace(exifErrors))
            {
                Interlocked.Increment(ref counters.Errors);
                errors.Add("ExifTool reported issues:" + Environment.NewLine + exifErrors.Trim());
            }
        }

        return new ProcessingReport
        {
            TotalMedia = media.Count,
            Matched = counters.Matched,
            Unmatched = counters.Unmatched,
            Duplicates = counters.Duplicates,
            MetadataWritten = counters.MetadataWritten,
            SpecialFolderItems = counters.SpecialItems,
            Errors = counters.Errors,
            Cancelled = ct.IsCancellationRequested,
            DryRun = options.DryRun,
            ErrorMessages = errors.ToArray(),
            Outcomes = outcomes.ToArray(),
        };
    }

    private static async Task<FileOutcome> ProcessOneAsync(
        MatchResult match, ProcessingOptions options, TakeoutArchiveReader reader,
        DateResolver dateResolver, TimezoneResolver tz, OutputPathBuilder pathBuilder,
        HashDeduplicator dedup, AlbumLinker linker, ExifToolPool? exifPool,
        Counters counters, bool dryRun, CancellationToken ct)
    {
        var media = match.Media;

        TakeoutJson? json = null;
        if (match.Sidecar is { } sidecar)
        {
            var bytes = await reader.ReadAllBytesAsync(sidecar, ct).ConfigureAwait(false);
            json = TakeoutJson.Parse(System.Text.Encoding.UTF8.GetString(bytes));
        }

        var resolved = dateResolver.Resolve(media.FileName, json, exifDateLocal: null, media.Folder, fileModifiedUtc: null);

        // Compute local capture time + offset for metadata and for the year/month folder.
        DateTime? localDate = null, utcDate = null;
        string? offset = null;
        if (resolved.HasValue)
        {
            if (resolved.IsUtc)
            {
                var instant = new DateTimeOffset(resolved.Value, TimeSpan.Zero);
                var local = tz.ResolveLocal(instant, json?.BestGeo);
                localDate = local.DateTime;
                utcDate = instant.UtcDateTime;
                offset = local.Offset == TimeSpan.Zero && json?.BestGeo is not { IsPresent: true }
                    ? null
                    : FormatOffset(local.Offset);
            }
            else
            {
                localDate = resolved.Value; // already local wall-clock
                utcDate = resolved.Value;
            }
        }

        var isSpecial = OutputPathBuilder.Classify(media.Folder) != SpecialFolder.None;
        if (isSpecial) Interlocked.Increment(ref counters.SpecialItems);

        var dest = pathBuilder.BuildPath(options.OutputDirectory, media, localDate);

        var outcome = new FileOutcome
        {
            FileName = media.FileName,
            SourceFolder = media.Folder,
            Matched = match.IsMatched,
            DateSource = resolved.HasValue ? resolved.Source.ToString() : null,
            DestinationPath = dest,
        };

        // Dry-run: everything above is pure planning — stop before touching the disk.
        if (dryRun)
            return outcome with { Planned = true };

        // Album shortcut handling: an album copy that duplicates the main-library
        // copy becomes a link to the canonical file instead of a second extraction.
        var isAlbumCopy = !isSpecial
                          && !OutputPathBuilder.IsMainLibraryFolder(media.Folder)
                          && options.OutputStructure == OutputStructure.YearMonth;

        // Extract to a per-entry temp file first so dedup can decide before we ever
        // touch the canonical destination (two copies can map to the same dest path).
        // The content hash is computed during extraction — no extra disk read.
        var tempPath = dest + "." + StableToken(media.ArchiveId + "|" + media.Path) + ".part";

        try
        {
            var extract = await reader.ExtractAsync(media, tempPath, ct).ConfigureAwait(false);

            // De-duplication is an atomic claim on the content hash: exactly one media
            // file owns a given hash and produces the canonical file; identical files
            // are duplicates that resolve the owner's final path by awaiting it.
            if (options.DuplicateHandling == DuplicateHandling.KeepBest)
            {
                var claim = dedup.TryClaim(extract.ContentHash);
                if (!claim.IsOwner)
                {
                    string canonical;
                    try
                    {
                        canonical = await claim.CanonicalPath!.ConfigureAwait(false);
                    }
                    catch
                    {
                        // The owner failed to materialize its file — salvage this copy
                        // instead of discarding it (avoids losing the only remaining one).
                        var (salvageDest, salvageWrote) = await PlaceAndTagAsync(
                            tempPath, dest, json, localDate, utcDate, offset, exifPool, counters, ct).ConfigureAwait(false);
                        return outcome with { DestinationPath = salvageDest, MetadataWritten = salvageWrote };
                    }

                    Interlocked.Increment(ref counters.Duplicates);
                    TryDelete(tempPath);

                    if (isAlbumCopy && options.AlbumStrategy == AlbumStrategy.Shortcut)
                    {
                        var albumName = LastSegment(media.Folder);
                        var linkPath = Path.Combine(options.OutputDirectory, "Albums", albumName, media.FileName);
                        linker.Link(canonical, linkPath);
                    }
                    return outcome with { IsDuplicate = true, DestinationPath = canonical };
                }

                // We own this content: place the canonical file and publish its path so
                // waiting duplicates can link to it. Publish/fail must always happen.
                var published = false;
                try
                {
                    var finalDest = MoveUnique(tempPath, dest);
                    dedup.PublishOwnerPath(extract.ContentHash, finalDest);
                    published = true;
                    var wrote = await TagAsync(finalDest, json, localDate, utcDate, offset, exifPool, counters, ct)
                        .ConfigureAwait(false);
                    return outcome with { DestinationPath = finalDest, MetadataWritten = wrote };
                }
                finally
                {
                    if (!published) dedup.FailOwner(extract.ContentHash);
                }
            }

            // KeepAll: no de-duplication, just place and tag every copy.
            var (placedDest, placedWrote) = await PlaceAndTagAsync(
                tempPath, dest, json, localDate, utcDate, offset, exifPool, counters, ct).ConfigureAwait(false);
            return outcome with { DestinationPath = placedDest, MetadataWritten = placedWrote };
        }
        finally
        {
            // Never leave an orphaned .part behind (the happy paths move/delete it,
            // but an exception mid-flight would otherwise litter the output tree).
            if (LongPath.Exists(tempPath)) TryDelete(tempPath);
        }
    }

    private static async Task<(string dest, bool metadataWritten)> PlaceAndTagAsync(
        string tempPath, string dest, TakeoutJson? json, DateTime? localDate, DateTime? utcDate,
        string? offset, ExifToolPool? exifPool, Counters counters, CancellationToken ct)
    {
        var finalDest = MoveUnique(tempPath, dest);
        var wrote = await TagAsync(finalDest, json, localDate, utcDate, offset, exifPool, counters, ct).ConfigureAwait(false);
        return (finalDest, wrote);
    }

    private static async Task<bool> TagAsync(
        string finalDest, TakeoutJson? json, DateTime? localDate, DateTime? utcDate, string? offset,
        ExifToolPool? exifPool, Counters counters, CancellationToken ct)
    {
        if (exifPool is null)
            return false;

        var meta = BuildMetadata(json, localDate, utcDate, offset);
        if (meta.IsEmpty)
            return false;

        await exifPool.WriteAsync(finalDest, meta, ct).ConfigureAwait(false);
        Interlocked.Increment(ref counters.MetadataWritten);
        return true;
    }

    /// <summary>
    /// Moves a temp file to a free destination name, retrying if another thread
    /// claims the same name first. Without the retry, a lost race throws and the
    /// media file would be dropped (data loss).
    /// </summary>
    private const int MaxRenameAttempts = 100_000;

    private static string MoveUnique(string tempPath, string dest)
    {
        for (var attempt = 0; attempt < MaxRenameAttempts; attempt++)
        {
            var finalDest = MakeUnique(dest);
            try
            {
                LongPath.Move(tempPath, finalDest, overwrite: false);
                return finalDest;
            }
            catch (IOException) when (LongPath.Exists(finalDest))
            {
                // Another thread won this name between MakeUnique and Move; pick the next.
            }
        }

        throw new IOException(
            $"Could not move '{tempPath}' to a free name near '{dest}' after {MaxRenameAttempts} attempts.");
    }

    /// <summary>Returns dest if free, otherwise appends " (1)", " (2)", ... before the extension.</summary>
    private static string MakeUnique(string dest)
    {
        if (!LongPath.Exists(dest))
            return dest;

        var dir = Path.GetDirectoryName(dest)!;
        var stem = Path.GetFileNameWithoutExtension(dest);
        var ext = Path.GetExtension(dest);
        for (var i = 1; i <= MaxRenameAttempts; i++)
        {
            var candidate = Path.Combine(dir, $"{stem} ({i}){ext}");
            if (!LongPath.Exists(candidate))
                return candidate;
        }

        throw new IOException(
            $"Could not find a free name for '{dest}' after {MaxRenameAttempts} attempts.");
    }

    private static string StableToken(string key)
    {
        var bytes = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(key));
        return Convert.ToHexString(bytes, 0, 4);
    }

    private static ExifMetadata BuildMetadata(TakeoutJson? json, DateTime? local, DateTime? utc, string? offset) =>
        new()
        {
            DateTakenLocal = local,
            DateTakenUtc = utc,
            Offset = offset,
            Latitude = json?.BestGeo?.Latitude,
            Longitude = json?.BestGeo?.Longitude,
            Altitude = json?.BestGeo is { IsPresent: true } g ? g.Altitude : null,
            Description = string.IsNullOrWhiteSpace(json?.Description) ? null : json!.Description,
            Favorited = json?.Favorited ?? false,
        };

    private static string FormatOffset(TimeSpan offset) =>
        (offset < TimeSpan.Zero ? "-" : "+") + offset.ToString(@"hh\:mm", System.Globalization.CultureInfo.InvariantCulture);

    private static void TryDelete(string path)
    {
        try { File.Delete(LongPath.Extended(path)); } catch { /* best effort */ }
    }

    private static readonly char[] PathSeparators = ['/', '\\'];

    private static string LastSegment(string folder)
    {
        var trimmed = folder.TrimEnd(PathSeparators);
        var idx = trimmed.LastIndexOfAny(PathSeparators);
        return idx >= 0 ? trimmed[(idx + 1)..] : trimmed;
    }

    private sealed class Counters
    {
        public int Matched;
        public int Unmatched;
        public int Duplicates;
        public int MetadataWritten;
        public int SpecialItems;
        public int Errors;
    }
}
