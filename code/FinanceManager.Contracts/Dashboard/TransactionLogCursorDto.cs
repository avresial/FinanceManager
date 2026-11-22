using FinanceManager.Contracts.FinancialAccounts.Shared;

namespace FinanceManager.Contracts.Dashboard;

public record TransactionLogCursorDto(DateTime Date, AccountTypeDto AccountType, long EntryId);