using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace NTS.Tests.Integration.Infrastructure;

/// <summary>
/// Keeps what a host logs, for the tests that look at what is written about an event and at what is not. Add it to the
/// services of the host as an <see cref="ILoggerProvider"/>.
/// </summary>
internal sealed class LogCapture : ILoggerProvider
{
    readonly ConcurrentQueue<LogEntry> _entries = new();

    public IReadOnlyList<LogEntry> Entries => [.. _entries];

    /// <summary>The entries of an event, by the name it is logged under.</summary>
    public IReadOnlyList<LogEntry> Of(string eventName)
    {
        return [.. _entries.Where(x => x.EventId.Name == eventName)];
    }

    public ILogger CreateLogger(string categoryName)
    {
        return new CaptureLogger(categoryName, _entries);
    }

    public void Dispose() { }

    sealed class CaptureLogger : ILogger
    {
        readonly string _category;
        readonly ConcurrentQueue<LogEntry> _entries;

        public CaptureLogger(string category, ConcurrentQueue<LogEntry> entries)
        {
            _category = category;
            _entries = entries;
        }

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull
        {
            return null;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return logLevel != LogLevel.None;
        }

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter
        )
        {
            _entries.Enqueue(new LogEntry(_category, logLevel, eventId, formatter(state, exception), exception));
        }
    }
}

internal sealed class LogEntry
{
    public LogEntry(string category, LogLevel level, EventId eventId, string message, Exception? exception)
    {
        Category = category;
        Level = level;
        EventId = eventId;
        Message = message;
        Exception = exception;
    }

    public string Category { get; }
    public LogLevel Level { get; }
    public EventId EventId { get; }
    public string Message { get; }
    public Exception? Exception { get; }

    /// <summary>Everything the entry says, the exception included: what has to be free of a secret.</summary>
    public string Text => Message + Exception;
}
