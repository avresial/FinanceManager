using System.Collections.Concurrent;

namespace FinanceManager.Api.Logging;

[ProviderAlias("Database")]
public sealed class DatabaseLoggerProvider(ILogEntryQueue queue) : ILoggerProvider
{
    private readonly ConcurrentDictionary<string, DatabaseLogger> _loggers = new();

    public ILogger CreateLogger(string categoryName) =>
        _loggers.GetOrAdd(categoryName, name => new DatabaseLogger(name, queue));

    public void Dispose() => _loggers.Clear();
}