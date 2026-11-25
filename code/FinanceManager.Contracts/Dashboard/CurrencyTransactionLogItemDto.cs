using FinanceManager.Contracts.FinancialAccounts.Shared;

namespace FinanceManager.Contracts.Dashboard;

public record CurrencyTransactionLogItemDto(int? LegacyAccountId, long EntryId, DateTime PostingDate, decimal ValueChange,
    string Description, int CurrencyId, string? Currency, string? ContractorDetails, IReadOnlyList<FinancialLabelDto> Labels)
    : TransactionLogItemDto(LegacyAccountId);