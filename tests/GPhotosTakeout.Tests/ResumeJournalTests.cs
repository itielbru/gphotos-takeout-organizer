using GPhotosTakeout.Core.Pipeline;
using Xunit;

namespace GPhotosTakeout.Tests;

public class ResumeJournalTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "gphjournal_" + Guid.NewGuid().ToString("N"));

    public ResumeJournalTests() => Directory.CreateDirectory(_dir);

    [Fact]
    public void MarkDone_PersistsAcrossReopen()
    {
        var key = "archive.zip|Takeout/Google Photos/Photos from 2023/IMG.jpg";

        using (var j = ResumeJournal.Open(_dir))
        {
            Assert.False(j.IsDone(key));
            j.MarkDone(key);
            Assert.True(j.IsDone(key));
        }

        using var j2 = ResumeJournal.Open(_dir);
        Assert.True(j2.IsDone(key));
    }

    [Fact]
    public void CompletedCount_ReflectsDistinctEntries()
    {
        using var j = ResumeJournal.Open(_dir);
        j.MarkDone("key-1");
        j.MarkDone("key-2");
        j.MarkDone("key-1"); // duplicate
        Assert.Equal(2, j.CompletedCount);
    }

    [Fact]
    public void ReopenAfterDelete_StartsFromZero()
    {
        using (var j = ResumeJournal.Open(_dir))
        {
            j.MarkDone("key-a");
            j.MarkDone("key-b");
        }

        File.Delete(Path.Combine(_dir, ResumeJournal.FileName));

        using var j2 = ResumeJournal.Open(_dir);
        Assert.Equal(0, j2.CompletedCount);
        Assert.False(j2.IsDone("key-a"));
    }

    [Fact]
    public void CorruptLine_IsIgnoredOnReopen()
    {
        // Write a valid line then a partial/zero-length line (as a disrupted write would produce).
        using (var j = ResumeJournal.Open(_dir))
            j.MarkDone("valid-key");

        // Append a corrupted (empty) line; the reader already handles blank lines gracefully.
        var logPath = Path.Combine(_dir, ResumeJournal.FileName);
        File.AppendAllText(logPath, "\n\n");

        using var j2 = ResumeJournal.Open(_dir);
        Assert.True(j2.IsDone("valid-key"));
        Assert.Equal(1, j2.CompletedCount);
    }

    [Fact]
    public void ThreadSafety_ConcurrentMarkDone_NoDataLoss()
    {
        const int threads = 20;
        const int keysPerThread = 50;

        using var j = ResumeJournal.Open(_dir);
        var tasks = Enumerable.Range(0, threads).Select(t => Task.Run(() =>
        {
            for (var i = 0; i < keysPerThread; i++)
                j.MarkDone($"thread{t}-key{i}");
        })).ToArray();

        Task.WaitAll(tasks);

        // Every unique key must be recorded exactly once.
        Assert.Equal(threads * keysPerThread, j.CompletedCount);
        for (var t = 0; t < threads; t++)
            for (var i = 0; i < keysPerThread; i++)
                Assert.True(j.IsDone($"thread{t}-key{i}"));
    }

    [Fact]
    public void ThreadSafety_WrittenKeysPersistedAfterReopen()
    {
        const int threads = 10;
        const int keysPerThread = 20;

        using (var j = ResumeJournal.Open(_dir))
        {
            var tasks = Enumerable.Range(0, threads).Select(t => Task.Run(() =>
            {
                for (var i = 0; i < keysPerThread; i++)
                    j.MarkDone($"t{t}-k{i}");
            })).ToArray();
            Task.WaitAll(tasks);
        }

        using var j2 = ResumeJournal.Open(_dir);
        Assert.Equal(threads * keysPerThread, j2.CompletedCount);
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { /* ignore */ }
    }
}
