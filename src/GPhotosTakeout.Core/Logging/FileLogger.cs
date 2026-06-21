using System.Globalization;
using System.Text;
using Microsoft.Extensions.Logging;

namespace GPhotosTakeout.Core.Logging;

/// <summary>
/// A minimal, dependency-free <see cref="ILoggerProvider"/> that appends one line per
/// log entry to a file. Both the WinUI app and the CLI use it so a run leaves a durable,
/// timestamped trace on disk without pulling in a third-party logging framework.
/// </summary>
public sealed class FileLoggerProvider : ILoggerProvider
{
    private readonly StreamWriter _writer;
    private readonly Lock _gate = new();
    private readonly LogLevel _minLevel;

    public FileLoggerProvider(string path, LogLevel minLevel = LogLevel.Information)
    {
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);
        _writer = new StreamWriter(path, append: true, Encoding.UTF8) { AutoFlush = true };
        _minLevel = minLevel;
    }

    public ILogger CreateLogger(string categoryName) => new FileLogger(categoryName, this);

    internal void Write(string line)
    {
        lock (_gate)
            _writer.WriteLine(line);
    }

    internal bool IsEnabled(LogLevel level) => level >= _minLevel && level != LogLevel.None;

    public void Dispose()
    {
        lock (_gate)
            _writer.Dispose();
    }

    private sealed class FileLogger(string category, FileLoggerProvider provider) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => provider.IsEnabled(logLevel);

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
                return;

            var shortCategory = category.Contains('.', StringComparison.Ordinal)
                ? category[(category.LastIndexOf('.') + 1)..]
                : category;
            var ts = DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture);
            var sb = new StringBuilder()
                .Append(ts).Append(" [").Append(Level(logLevel)).Append("] ")
                .Append(shortCategory).Append(": ")
                .Append(formatter(state, exception));
            if (exception is not null)
                sb.Append(" | ").Append(exception.GetType().Name).Append(": ").Append(exception.Message);
            provider.Write(sb.ToString());
        }

        private static string Level(LogLevel l) => l switch
        {
            LogLevel.Trace => "TRC",
            LogLevel.Debug => "DBG",
            LogLevel.Information => "INF",
            LogLevel.Warning => "WRN",
            LogLevel.Error => "ERR",
            LogLevel.Critical => "CRT",
            _ => "???",
        };
    }
}
