using FinanceManager.Domain.Entities;
using FinanceManager.Domain.Repositories;
using System.Runtime.CompilerServices;

namespace FinanceManager.Infrastructure.Repositories;
public class CurrencyRepository : ICurrencyRepository
{
    public async IAsyncEnumerable<Currency> GetCurrencies([EnumeratorCancellation] CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        yield return DefaultCurrency.PLN;
        yield return DefaultCurrency.USD;

        await Task.CompletedTask;
    }
}
