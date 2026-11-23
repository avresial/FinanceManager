using FinanceManager.Domain.FinancialAccounts.Shared.Entities;

namespace FinanceManager.Domain.Transactions.Cash.Dtos;

public record CashLogEntry(int? LegacyAccountId, long EntryId, DateTime PostingDate, decimal ValueChange,
    string Description, int CurrencyId, string? Currency, string? ContractorDetails, IReadOnlyList<FinancialLabel> Labels);