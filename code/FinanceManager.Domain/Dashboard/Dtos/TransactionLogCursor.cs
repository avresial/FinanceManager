namespace FinanceManager.Domain.Dashboard.Dtos;

public record TransactionLogCursor(DateTime Date, TransactionKind Kind, long EntryId);