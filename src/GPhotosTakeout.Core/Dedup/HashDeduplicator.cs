using System.Collections.Concurrent;
using System.Security.Cryptography;
using GPhotosTakeout.Core.IO;

namespace GPhotosTakeout.Core.Dedup;

/// <summary>
/// Detects identical files by full content hash. The hash is computed once, while
/// the file is being extracted (see <c>TakeoutArchiveReader.ExtractAsync</c>), so
/// the "is this a duplicate?" decision costs no extra disk reads.
///
/// The class is concurrency-safe and race-free by construction: the only shared
/// state is an in-memory map keyed by content hash, and no method ever touches the
/// filesystem on behalf of another thread. The first file to <see cref="Claim"/> a
/// given hash becomes its owner and must <see cref="PublishOwnerPath"/> the final
/// location it ends up at; later identical files are duplicates that resolve the
/// owner's canonical path by awaiting it.
/// </summary>
public sealed class HashDeduplicator
{
    // content hash -> completion that yields the owner's final on-disk path.
    private readonly ConcurrentDictionary<string, TaskCompletionSource<string>> _byHash = new(StringComparer.Ordinal);

    /// <summary>The result of trying to claim ownership of a content hash.</summary>
    public readonly record struct Claim(bool IsOwner, Task<string>? CanonicalPath);

    /// <summary>
    /// Attempts to become the owner of <paramref name="contentHash"/>. Exactly one
    /// caller per hash gets <c>IsOwner = true</c> and must later call
    /// <see cref="PublishOwnerPath"/> (on success) or <see cref="FailOwner"/> (on
    /// failure). Every other caller gets <c>IsOwner = false</c> and a task that
    /// completes with the owner's canonical path once it is known.
    /// </summary>
    public Claim TryClaim(string contentHash)
    {
        var mine = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var winner = _byHash.GetOrAdd(contentHash, mine);
        return ReferenceEquals(winner, mine)
            ? new Claim(IsOwner: true, CanonicalPath: null)
            : new Claim(IsOwner: false, CanonicalPath: winner.Task);
    }

    /// <summary>Records the final path the owner of <paramref name="contentHash"/> wrote to.</summary>
    public void PublishOwnerPath(string contentHash, string finalPath)
    {
        if (_byHash.TryGetValue(contentHash, out var tcs))
            tcs.TrySetResult(finalPath);
    }

    /// <summary>
    /// Signals that the owner failed to produce a file for <paramref name="contentHash"/>.
    /// The entry is removed so a later identical file can re-own it, and any waiters
    /// observe a faulted task (and can salvage their own copy instead of losing data).
    /// </summary>
    public void FailOwner(string contentHash)
    {
        if (_byHash.TryRemove(contentHash, out var tcs))
            tcs.TrySetException(new IOException($"The owning copy for content {contentHash[..Math.Min(8, contentHash.Length)]}… failed to materialize."));
    }

    /// <summary>
    /// File-based convenience used outside the pipeline (and by tests): hashes the
    /// file, then claims ownership. Returns the path of an already-kept identical
    /// file, or null if this file is new (and now registered under its own path).
    /// </summary>
    public string? FindDuplicateOrRegister(string filePath)
    {
        var hash = ComputeFullHash(filePath);
        var claim = TryClaim(hash);
        if (claim.IsOwner)
        {
            PublishOwnerPath(hash, filePath);
            return null;
        }
        return claim.CanonicalPath!.Result;
    }

    /// <summary>Full content SHA-256, as an upper-case hex string.</summary>
    public static string ComputeFullHash(string filePath)
    {
        using var stream = LongPath.OpenRead(filePath);
        using var sha = SHA256.Create();
        return Convert.ToHexString(sha.ComputeHash(stream));
    }
}
