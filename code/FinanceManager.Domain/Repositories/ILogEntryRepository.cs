using FinanceManager.Domain.Entities.Logs;
using FinanceManager.Domain.Enums;

namespace FinanceManager.Domain.Repositories;

public interface ILogEntryRepository
{
    Task AddRange(IEnumerable<LogEntry> entries, CancellationToken cancellationToken = default);

    Task<List<LogEntry>> GetLatest(int count, IReadOnlyCollection<LogSeverity>? levels = null, CancellationToken cancellationToken = default);

    Task<(List<LogEntry> Items, int TotalCount)> GetPaged(
        int skip,
        int take,
        IReadOnlyCollection<LogSeverity>? levels = null,
        CancellationToken cancellationToken = default);

    Task<int> DeleteOlderThan(DateTime cutoffUtc, CancellationToken cancellationToken = default);
}