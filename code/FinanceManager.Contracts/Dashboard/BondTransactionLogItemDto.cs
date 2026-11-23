using FinanceManager.Contracts.FinancialAccounts.Shared;

namespace FinanceManager.Contracts.Dashboard;

public record BondTransactionLogItemDto(int? LegacyAccountId, long EntryId, DateTime PostingDate, decimal ValueChange,
    string Description, int BondDetailsId, string? Currency, string? BondName, IReadOnlyList<FinancialLabelDto> Labels)
    : TransactionLogItemDto(LegacyAccountId);