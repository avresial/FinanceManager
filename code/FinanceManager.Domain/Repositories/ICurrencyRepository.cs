using FinanceManager.Domain.Entities.Currencies;

namespace FinanceManager.Domain.Repositories;

public interface ICurrencyRepository
{
    public IAsyncEnumerable<Currency> GetCurrencies(CancellationToken ct = default);
    public Task<Currency?> GetCurrency(int id, CancellationToken ct = default);
    public Task<Currency?> GetByCode(string shortName, CancellationToken ct = default);
    public Task<Currency> GetOrAdd(string shortName, string? symbol, CancellationToken ct = default);
}