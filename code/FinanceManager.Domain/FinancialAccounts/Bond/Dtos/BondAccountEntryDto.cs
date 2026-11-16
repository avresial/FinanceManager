using FinanceManager.Domain.FinancialAccounts.Bond.Entities;
using FinanceManager.Domain.FinancialAccounts.Shared.Entities;

namespace FinanceManager.Domain.FinancialAccounts.Bond.Dtos;

public class BondAccountEntryDto
{
    public int BondDetailsId { get; set; }
    public string Description { get; set; } = string.Empty;

    public long EntryId { get; set; }
    public int AccountId { get; set; }
    public DateTime PostingDate { get; set; }
    public decimal Value { get; set; }
    public decimal ValueChange { get; set; }
    public List<FinancialLabel> Labels { get; set; } = [];

    public BondAccountEntry ToBondAccountEntry() => new(AccountId, EntryId, PostingDate, Value, ValueChange, BondDetailsId)
    {
        Description = Description,
    };
}