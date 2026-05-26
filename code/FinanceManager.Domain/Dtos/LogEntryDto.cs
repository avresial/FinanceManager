using FinanceManager.Domain.Enums;

namespace FinanceManager.Domain.Dtos;

public record LogEntryDto(
    int Id,
    DateTime TimestampUtc,
    LogSeverity Level,
    string Category,
    string Message,
    string? Exception,
    int? EventId,
    string? EventName);

public record PagedLogEntriesDto(IReadOnlyList<LogEntryDto> Items, int TotalCount);