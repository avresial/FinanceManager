using FinanceManager.Domain.Entities.FinancialAccounts.Currencies;
using FinanceManager.Infrastructure.Dtos;

namespace FinanceManager.Domain.Dtos;

public class CurrencyAccountEntryDto : FinancialEntryBaseDto
{
    public string Description { get; set; } = string.Empty;

    public CurrencyAccountEntry ToCurrencyAccountEntry() => new(AccountId, EntryId, PostingDate, Value, ValueChange)
    {
        Description = Description,
    };
}