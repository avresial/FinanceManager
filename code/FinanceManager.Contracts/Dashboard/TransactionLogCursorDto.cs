namespace FinanceManager.Contracts.Dashboard;

public record TransactionLogCursorDto(DateTime Date, TransactionKindDto Kind, long EntryId);