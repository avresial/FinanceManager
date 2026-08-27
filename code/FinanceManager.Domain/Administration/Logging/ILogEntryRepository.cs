using FinanceManager.Domain.Identity.Entities;

namespace FinanceManager.Domain.Administration.Logging;

public interface ILogEntryRepository
{
    Task AddRange(IEnumerable<LogEntry> entries, CancellationToken cancellationToken = default);

    Task<List<LogEntry>> GetLatest(int count, IReadOnlyCollection<LogSeverity>? levels = null, CancellationToken cancellationToken = default);

    Task<(List<LogEntry> Items, int TotalCount)> GetPaged(
        int skip,
        int take,
        IReadOnlyCollection<LogSeverity>? levels = null,
        CancellationToken cancellationToken = default);

    Task<(List<LogEntry> Items, int TotalCount)> GetPaged(
        int skip,
        int take,
        IReadOnlyCollection<LogSeverity>? levels,
        DateTime? fromUtc,
        DateTime? toUtc,
        string? search,
        CancellationToken cancellationToken = default);

    Task<int> DeleteOlderThan(DateTime cutoffUtc, CancellationToken cancellationToken = default);
}