namespace FinanceManager.Domain.Services;
public interface ICurrencyExchangeService
{
    public Task<decimal?> GetExchangeRateAsync(string fromCurrency, string toCurrency, DateTime date);
}
