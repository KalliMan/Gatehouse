using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace Gatehouse.Accounts.Tests;

/// <summary>A logger provider that keeps every entry: the formatted message and the attached exception, if any.</summary>
public sealed class LogCapture : ILoggerProvider
{
    public ConcurrentQueue<(string Message, Exception? Exception)> Entries { get; } = new();

    public ILogger CreateLogger(string categoryName) => new Logger(this);

    public void Dispose()
    {
    }

    private sealed class Logger(LogCapture capture) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter) =>
            capture.Entries.Enqueue((formatter(state, exception), exception));
    }
}
