namespace FinanceManager.Infrastructure.Dtos;

public class ImportStockModel
{
    public DateTime PostingDate { get; set; }
    public decimal ValueChange { get; set; }
}