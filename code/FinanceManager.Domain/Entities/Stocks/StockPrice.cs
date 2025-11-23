using FinanceManager.Domain.Entities.Currencies;

namespace FinanceManager.Domain.Entities.Stocks;

public class StockPrice
{
    public required string Ticker { get; set; }
    public decimal PricePerUnit { get; set; }
    public required Currency Currency { get; set; }
    public DateTime Date { get; set; }
}
