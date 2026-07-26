using FinanceManager.Domain.Administration.Logging;
using FinanceManager.Domain.Identity.Entities;

namespace FinanceManager.Api.Features.Administration.Logging;

public sealed class DatabaseLogger(string category, ILogEntryQueue queue) : ILogger
{
    // Prevent recursive logging: any log written by the persistence/broadcast pipeline
    // (EF Core, SignalR, our own services) would otherwise re-enter the queue.
    private static readonly AsyncLocal<bool> _suppressed = new();

    public static IDisposable BeginSuppression()
    {
        _suppressed.Value = true;
        return new SuppressionScope();
    }

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) =>
        logLevel >= LogLevel.Warning && logLevel != LogLevel.None && !_suppressed.Value;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel)) return;
        if (formatter is null) return;

        var message = formatter(state, exception);
        if (string.IsNullOrEmpty(message) && exception is null) return;

        var entry = new LogEntry
        {
            TimestampUtc = DateTime.UtcNow,
            Level = (LogSeverity)logLevel,
            Category = Truncate(category, 256),
            Message = Truncate(message ?? string.Empty, 4096),
            Exception = exception?.ToString(),
            EventId = eventId.Id == 0 ? null : eventId.Id,
            EventName = string.IsNullOrEmpty(eventId.Name) ? null : Truncate(eventId.Name, 256),
        };

        // The queue is bounded with FullMode = DropOldest, so TryEnqueue only fails
        // when the channel is closed (process shutdown). We intentionally don't
        // surface that — a logger that throws or re-logs from inside Log() would
        // either crash callers or recurse. Dropping the oldest pending entry under
        // sustained load is the explicit trade-off for back-pressure-free logging.
        queue.TryEnqueue(entry);
    }

    private static string Truncate(string value, int max) =>
        value.Length <= max ? value : value[..max];

    private sealed class SuppressionScope : IDisposable
    {
        public void Dispose() => _suppressed.Value = false;
    }
}