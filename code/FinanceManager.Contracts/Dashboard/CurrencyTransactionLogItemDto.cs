using FinanceManager.Contracts.Labels;

namespace FinanceManager.Contracts.Dashboard;

public record CurrencyTransactionLogItemDto(long EntryId, DateTime PostingDate, decimal ValueChange,
    string Description, int CurrencyId, string? Currency, string? ContractorDetails, IReadOnlyList<FinancialLabelDto> Labels)
    : TransactionLogItemDto;