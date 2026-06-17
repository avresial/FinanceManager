using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;

namespace FinanceManager.Domain.FinancialAccounts.Stock.Entities;

public class StockPrice
{
    public required string Isin { get; set; }
    public decimal PricePerUnit { get; set; }
    public required Currency Currency { get; set; }
    public DateTime Date { get; set; }
}