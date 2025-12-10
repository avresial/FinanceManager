using FinanceManager.Domain.Entities.Bonds;

namespace FinanceManager.Infrastructure.Dtos;

public class BondAccountEntryDto : FinancialEntryBaseDto
{
    public int BondDetailsId { get; set; }

    public BondAccountEntry ToBondAccountEntry() => new(AccountId, EntryId, PostingDate, Value, ValueChange, BondDetailsId);
}