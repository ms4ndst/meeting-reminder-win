using System.Collections.Concurrent;
using System.IO;
using Microsoft.Extensions.Logging;

namespace MeetingReminder.App.Services;

/// <summary>
/// Tiny dependency-free rolling-file logger. One file per day, kept under
/// %LOCALAPPDATA%\MeetingReminder\logs. Older than 30 days gets pruned at startup.
/// Also exposes a LogLine event so the in-app Log tab can subscribe live.
/// </summary>
public sealed class RollingFileLoggerProvider : ILoggerProvider
{
    public static event EventHandler<string>? LogLine;

    private readonly string _logDir;
    private readonly object _writeLock = new();
    private readonly ConcurrentDictionary<string, RollingFileLogger> _loggers = new();

    public RollingFileLoggerProvider(string logDir)
    {
        _logDir = logDir;
        Directory.CreateDirectory(logDir);
        PruneOldLogs();
    }

    public ILogger CreateLogger(string categoryName) =>
        _loggers.GetOrAdd(categoryName, name => new RollingFileLogger(name, this));

    public void Dispose() => _loggers.Clear();

    public string CurrentLogFile =>
        Path.Combine(_logDir, $"meetingreminder-{DateTime.Now:yyyy-MM-dd}.log");

    internal void Write(string line)
    {
        lock (_writeLock)
        {
            try
            {
                File.AppendAllText(CurrentLogFile, line + Environment.NewLine);
            }
            catch { }
        }
        LogLine?.Invoke(this, line);
    }

    private void PruneOldLogs()
    {
        try
        {
            var cutoff = DateTime.UtcNow.AddDays(-30);
            foreach (var f in Directory.EnumerateFiles(_logDir, "meetingreminder-*.log"))
            {
                if (File.GetLastWriteTimeUtc(f) < cutoff)
                {
                    try { File.Delete(f); }
                    catch { }
                }
            }
        }
        catch { }
    }

    private sealed class RollingFileLogger : ILogger
    {
        private readonly string _category;
        private readonly RollingFileLoggerProvider _provider;

        public RollingFileLogger(string category, RollingFileLoggerProvider provider)
        {
            _category = category;
            _provider = provider;
        }

        public IDisposable BeginScope<TState>(TState state) where TState : notnull => NoopScope.Instance;
        public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Information;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
            Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel)) return;

            var msg = formatter(state, exception);
            var line = $"{DateTime.Now:HH:mm:ss.fff} [{logLevel,-11}] {_category}: {msg}";
            if (exception is not null)
                line += Environment.NewLine + exception;

            _provider.Write(line);
        }

        private sealed class NoopScope : IDisposable
        {
            public static readonly NoopScope Instance = new();
            public void Dispose() { }
        }
    }
}
