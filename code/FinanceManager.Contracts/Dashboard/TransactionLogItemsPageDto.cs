namespace FinanceManager.Contracts.Dashboard;

public record TransactionLogItemsPageDto(IReadOnlyList<TransactionLogItemDto> Items, TransactionLogCursorDto? NextCursor);