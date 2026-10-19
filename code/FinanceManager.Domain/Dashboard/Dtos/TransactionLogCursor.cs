using FinanceManager.Domain.FinancialAccounts.Shared.Entities;

namespace FinanceManager.Domain.Dashboard.Dtos;

public record TransactionLogCursor(DateTime Date, AccountType AccountType, long EntryId);