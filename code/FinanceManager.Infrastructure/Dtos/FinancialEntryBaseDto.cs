namespace FinanceManager.Infrastructure.Dtos;

public class FinancialEntryBaseDto
{
    public int EntryId { get; set; }
    public int AccountId { get; set; }
    public DateTime PostingDate { get; set; }
    public decimal Value { get; set; }
    public decimal ValueChange { get; set; }
}
