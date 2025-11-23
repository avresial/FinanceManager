using FinanceManager.Domain.Entities.Currencies;

namespace FinanceManager.Domain.Repositories;
public interface ICurrencyRepository
{
    public IAsyncEnumerable<Currency> GetCurrencies(CancellationToken ct = default);
    public Task<Currency?> GetCurrency(int id, CancellationToken ct = default);
}
