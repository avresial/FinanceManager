using FinanceManager.Domain.FinancialAccounts.Bond.Entities;
using FinanceManager.Domain.FinancialAccounts.Shared.Dtos;

namespace FinanceManager.Domain.FinancialAccounts.Bond.Dtos;

public class BondAccountEntryDto : FinancialEntryBaseDto
{
    public int BondDetailsId { get; set; }
    public string Description { get; set; } = string.Empty;

    public BondAccountEntry ToBondAccountEntry() => new(AccountId, EntryId, PostingDate, Value, ValueChange, BondDetailsId)
    {
        Description = Description,
    };
}