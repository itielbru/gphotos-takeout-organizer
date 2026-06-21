using GPhotosTakeout.Core.IO;

namespace GPhotosTakeout.Core.Pipeline;

/// <summary>
/// Append-only record of entries already completed, so an interrupted run can
/// resume. One entry key per line; loading is a set membership check. Stored in
/// the output directory as ".gphotos-resume.log".
/// </summary>
public sealed class ResumeJournal : IDisposable
{
    public const string FileName = ".gphotos-resume.log";

    private readonly string _path;
    private readonly HashSet<string> _done;
    private readonly StreamWriter _writer;
    private readonly object _gate = new();

    private ResumeJournal(string path, HashSet<string> done, StreamWriter writer)
    {
        _path = path;
        _done = done;
        _writer = writer;
    }

    public static ResumeJournal Open(string outputDirectory)
    {
        LongPath.EnsureDirectory(outputDirectory);
        var path = Path.Combine(outputDirectory, FileName);

        var done = new HashSet<string>(StringComparer.Ordinal);
        if (LongPath.Exists(path))
        {
            using var reader = new StreamReader(LongPath.OpenRead(path));
            string? line;
            while ((line = reader.ReadLine()) is not null)
                if (line.Length > 0) done.Add(line);
        }

        var stream = new FileStream(LongPath.Extended(path), FileMode.Append, FileAccess.Write, FileShare.Read);
        var writer = new StreamWriter(stream) { AutoFlush = true };
        return new ResumeJournal(path, done, writer);
    }

    // _done is a plain HashSet, so reads must take the same gate as writes: a
    // Contains() racing an Add() on the same set is undefined behavior.
    public bool IsDone(string key)
    {
        lock (_gate)
            return _done.Contains(key);
    }

    public void MarkDone(string key)
    {
        lock (_gate)
        {
            if (_done.Add(key))
                _writer.WriteLine(key);
        }
    }

    public int CompletedCount
    {
        get { lock (_gate) return _done.Count; }
    }

    public void Dispose()
    {
        lock (_gate)
            _writer.Dispose();
    }
}
