using System;
using System.IO;
using Microsoft.Extensions.Logging;

namespace PrintAgent.Logging;

internal sealed class FileLogger : ILogger
{
    private readonly string _categoryName;
    private readonly StreamWriter _writer;
    private readonly object _sync;
    private readonly LogLevel _minLevel;

    public FileLogger(string categoryName, StreamWriter writer, object sync, LogLevel minLevel)
    {
        _categoryName = categoryName;
        _writer = writer;
        _sync = sync;
        _minLevel = minLevel;
    }

    public IDisposable BeginScope<TState>(TState state) => NullScope.Instance;

    public bool IsEnabled(LogLevel logLevel) => logLevel >= _minLevel;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel) || formatter is null)
        {
            return;
        }

        var message = formatter(state, exception);
        if (string.IsNullOrWhiteSpace(message) && exception is null)
        {
            return;
        }

        var timestamp = DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
        lock (_sync)
        {
            _writer.WriteLine($"{timestamp} [{logLevel}] {_categoryName}: {message}");
            if (exception is not null)
            {
                _writer.WriteLine(exception);
            }
            _writer.Flush();
        }
    }
}

internal sealed class FileLoggerProvider : ILoggerProvider
{
    private readonly StreamWriter _writer;
    private readonly object _sync = new();
    private readonly LogLevel _minLevel;

    public FileLoggerProvider(string path, LogLevel minLevel)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        _writer = new StreamWriter(new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite))
        {
            AutoFlush = true
        };
        _minLevel = minLevel;
    }

    public ILogger CreateLogger(string categoryName) => new FileLogger(categoryName, _writer, _sync, _minLevel);

    public void Dispose() => _writer.Dispose();
}

internal sealed class NullScope : IDisposable
{
    public static NullScope Instance { get; } = new();

    private NullScope()
    {
    }

    public void Dispose()
    {
    }
}
