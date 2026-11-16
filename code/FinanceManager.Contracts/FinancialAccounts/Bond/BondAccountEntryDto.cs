using FinanceManager.Contracts.FinancialAccounts.Shared;

namespace FinanceManager.Contracts.FinancialAccounts.Bond;

public class BondAccountEntryDto
{
    public int BondDetailsId { get; set; }
    public string Description { get; set; } = string.Empty;

    public long EntryId { get; set; }
    public int AccountId { get; set; }
    public DateTime PostingDate { get; set; }
    public decimal Value { get; set; }
    public decimal ValueChange { get; set; }
    public List<FinancialLabelDto> Labels { get; set; } = [];

}