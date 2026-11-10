using Microsoft.Extensions.Logging;

namespace FocusLock.Tests.Fakes;

/// <summary>Collects log entries so tests can assert that something was logged.</summary>
public sealed class ListLogger<T> : ILogger<T>
{
    public List<LogEntry> Entries { get; } = [];

    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
        Entries.Add(new LogEntry(logLevel, formatter(state, exception), exception));

    public bool Has(LogLevel level, string messagePart) =>
        Entries.Any(entry => entry.Level == level && entry.Message.Contains(messagePart, StringComparison.OrdinalIgnoreCase));
}

public sealed record LogEntry(LogLevel Level, string Message, Exception? Exception);
