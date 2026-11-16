using FinanceManager.Contracts.FinancialAccounts.Shared;

namespace FinanceManager.Contracts.FinancialAccounts.Currencies;

public class CurrencyAccountEntryDto
{
    public string Description { get; set; } = string.Empty;
    public string? ContractorDetails { get; set; }

    public long EntryId { get; set; }
    public int AccountId { get; set; }
    public DateTime PostingDate { get; set; }
    public decimal Value { get; set; }
    public decimal ValueChange { get; set; }
    public List<FinancialLabelDto> Labels { get; set; } = [];

}