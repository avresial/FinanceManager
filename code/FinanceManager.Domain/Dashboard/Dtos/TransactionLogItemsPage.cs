namespace FinanceManager.Domain.Dashboard.Dtos;

public record TransactionLogItemsPage(IReadOnlyList<TransactionLogItem> Items, TransactionLogCursor? NextCursor);