using System.Text.Json;

namespace dDrive.Core.Diagnostics;

public sealed class FileAppLogger : IAppLogger
{
    private const long MaximumLogSize = 1_048_576;
    private const int RetainedLogCount = 3;
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);
    private readonly object _sync = new();
    private readonly string _filePath;

    public FileAppLogger(string? filePath = null)
    {
        _filePath = filePath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "dDrive",
            "logs",
            "dDrive.jsonl");
    }

            public string LogFilePath => _filePath;

    public void LogInformation(string eventName, string message) => Write("information", eventName, message, null);

    public void LogWarning(string eventName, string message) => Write("warning", eventName, message, null);

    public void LogError(string eventName, string message, Exception? exception = null) =>
        Write("error", eventName, message, exception);

    public void Clear()
    {
        lock (_sync)
        {
            for (var index = 0; index <= RetainedLogCount; index++)
            {
                var path = index == 0 ? _filePath : $"{_filePath}.{index}";
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
        }
    }

    private void Write(string level, string eventName, string message, Exception? exception)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(eventName);
        ArgumentNullException.ThrowIfNull(message);

        var directory = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var entry = JsonSerializer.SerializeToUtf8Bytes(new LogEntry(
            DateTimeOffset.UtcNow,
            level,
            eventName,
            message,
            exception?.GetType().FullName,
            exception?.Message), SerializerOptions);

        lock (_sync)
        {
            RotateIfNeeded(entry.Length + Environment.NewLine.Length);
            using var stream = new FileStream(_filePath, FileMode.Append, FileAccess.Write, FileShare.Read);
            stream.Write(entry);
            stream.Write("\n"u8);
        }
    }

    private void RotateIfNeeded(int incomingBytes)
    {
        if (!File.Exists(_filePath) || new FileInfo(_filePath).Length + incomingBytes <= MaximumLogSize)
        {
            return;
        }

        var oldest = $"{_filePath}.{RetainedLogCount}";
        if (File.Exists(oldest))
        {
            File.Delete(oldest);
        }

        for (var index = RetainedLogCount - 1; index >= 1; index--)
        {
            var source = $"{_filePath}.{index}";
            if (File.Exists(source))
            {
                File.Move(source, $"{_filePath}.{index + 1}", overwrite: true);
            }
        }

        File.Move(_filePath, $"{_filePath}.1", overwrite: true);
    }

    private sealed record LogEntry(
        DateTimeOffset TimestampUtc,
        string Level,
        string EventName,
        string Message,
        string? ExceptionType,
        string? ExceptionMessage);
}