using System.Buffers;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using FocusLock.Core.Abstractions;
using Microsoft.Extensions.Logging;

namespace FocusLock.Core.Logging;

/// <summary>Writes one JSON object per line: time, level, category, message, named properties and the exception.</summary>
internal sealed class RollingFileLogger(string category, RollingFileSink sink, LogLevel minimumLevel, IClock clock) : ILogger
{
    // Relaxed escaping keeps paths, timestamps and non-ASCII game names readable in the log file.
    private static readonly JsonWriterOptions WriterOptions = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None && logLevel >= minimumLevel;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel))
        {
            return;
        }

        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer, WriterOptions))
        {
            writer.WriteStartObject();
            writer.WriteString("time", clock.UtcNow.ToString("o"));
            writer.WriteString("level", logLevel.ToString());
            writer.WriteString("category", category);
            writer.WriteString("message", formatter(state, exception));
            WriteProperties(writer, state);
            if (exception is not null)
            {
                writer.WriteString("exception", exception.ToString());
            }

            writer.WriteEndObject();
        }

        sink.Write(Encoding.UTF8.GetString(buffer.WrittenSpan));
    }

    private static void WriteProperties<TState>(Utf8JsonWriter writer, TState state)
    {
        if (state is not IReadOnlyList<KeyValuePair<string, object?>> properties)
        {
            return;
        }

        var started = false;
        foreach (var property in properties)
        {
            if (property.Key == "{OriginalFormat}")
            {
                continue;
            }

            if (!started)
            {
                writer.WriteStartObject("props");
                started = true;
            }

            writer.WriteString(property.Key, property.Value?.ToString());
        }

        if (started)
        {
            writer.WriteEndObject();
        }
    }
}
