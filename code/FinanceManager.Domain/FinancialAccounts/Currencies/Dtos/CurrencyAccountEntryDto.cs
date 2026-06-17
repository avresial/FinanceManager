using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.FinancialAccounts.Shared.Dtos;
using FinanceManager.Infrastructure.Dtos;

namespace FinanceManager.Domain.FinancialAccounts.Currencies.Dtos;

public class CurrencyAccountEntryDto : FinancialEntryBaseDto
{
    public string Description { get; set; } = string.Empty;
    public string? ContractorDetails { get; set; }

    public CurrencyAccountEntry ToCurrencyAccountEntry() => new(AccountId, EntryId, PostingDate, Value, ValueChange)
    {
        Description = Description,
        ContractorDetails = ContractorDetails,
    };
}