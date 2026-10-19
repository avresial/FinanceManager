namespace FinanceManager.Domain.Dashboard.Dtos;

public record TransactionLogPage(IReadOnlyList<TransactionLogEntryDto> Items, TransactionLogCursor? NextCursor);