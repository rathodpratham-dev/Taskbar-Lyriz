using System.Text.Json;
using TaskbarLyriz.Core.Abstractions;
using TaskbarLyriz.Infrastructure.Storage;

namespace TaskbarLyriz.Infrastructure.Logging;

public sealed class JsonFileLogger : IAppLogger
{
    private const long MaximumFileSize = 5 * 1024 * 1024;
    private readonly object _gate = new();
    private readonly AppPaths _paths;
    private StreamWriter? _writer;
    private DateOnly _writerDate;
    private int _sequence;
    private bool _disposed;

    public JsonFileLogger(AppPaths paths)
    {
        _paths = paths;
    }

    public void Write(
        AppLogLevel level,
        string eventName,
        string message,
        Exception? exception = null,
        IReadOnlyDictionary<string, object?>? properties = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(eventName);
        ArgumentNullException.ThrowIfNull(message);

        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            EnsureWriter();

            var entry = new LogEntry(
                DateTimeOffset.UtcNow,
                level.ToString(),
                eventName,
                message,
                properties,
                exception?.ToString());

            _writer!.WriteLine(JsonSerializer.Serialize(entry));
            _writer.Flush();
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _writer?.Dispose();
            _writer = null;
        }
    }

    private void EnsureWriter()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        if (_writer is not null && _writerDate == today && _writer.BaseStream.Length < MaximumFileSize)
        {
            return;
        }

        _writer?.Dispose();
        Directory.CreateDirectory(_paths.LogsDirectory);

        if (_writerDate != today)
        {
            _sequence = 0;
        }

        _writerDate = today;
        string path;
        do
        {
            _sequence++;
            path = Path.Combine(
                _paths.LogsDirectory,
                $"taskbar-lyriz-{today:yyyyMMdd}-{_sequence:000}.jsonl");
        }
        while (File.Exists(path) && new FileInfo(path).Length >= MaximumFileSize);

        var stream = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.Read);
        _writer = new StreamWriter(stream);
    }

    private sealed record LogEntry(
        DateTimeOffset TimestampUtc,
        string Level,
        string EventName,
        string Message,
        IReadOnlyDictionary<string, object?>? Properties,
        string? Exception);
}
