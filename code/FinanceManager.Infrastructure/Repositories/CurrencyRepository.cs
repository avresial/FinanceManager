using FinanceManager.Domain.Entities;
using FinanceManager.Domain.Repositories;

namespace FinanceManager.Infrastructure.Repositories;
public class CurrencyRepository : ICurrencyRepository
{
    public async IAsyncEnumerable<Currency> GetCurrencies(CancellationToken ct = default)
    {
        yield return DefaultCurrency.PLN;
        yield return DefaultCurrency.USD;
    }
}
