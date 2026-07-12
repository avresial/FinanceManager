namespace FinanceManager.Domain.FinancialAccounts.Currencies.Entities;

public class ExchangeRate
{
    public long Id { get; set; }
    public required string FromCurrency { get; set; }
    public required string ToCurrency { get; set; }
    public DateTime Date { get; set; }
    public decimal Rate { get; set; }
}