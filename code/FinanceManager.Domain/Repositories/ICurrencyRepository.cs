using FinanceManager.Domain.Entities;

namespace FinanceManager.Domain.Repositories;
public interface ICurrencyRepository
{
    public IAsyncEnumerable<Currency> GetCurrencies(CancellationToken ct = default);
}
