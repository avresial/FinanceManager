namespace FinanceManager.Domain.Transactions.Shared.Dtos;

public record OwnerHistoryBoundary(DateTime Date, long EntryId, bool IncludeEqualId);