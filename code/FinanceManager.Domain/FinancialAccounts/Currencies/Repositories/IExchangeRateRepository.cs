namespace FinanceManager.Domain.FinancialAccounts.Currencies.Repositories;

public interface IExchangeRateRepository
{
    public Task<decimal?> Get(string fromCurrency, string toCurrency, DateTime date, CancellationToken ct = default);
    public Task Add(string fromCurrency, string toCurrency, DateTime date, decimal rate, CancellationToken ct = default);
}