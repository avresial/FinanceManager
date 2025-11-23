using FinanceManager.Domain.Entities.Currencies;
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

    public Task<Currency?> GetCurrency(int id, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        return Task.FromResult(id switch
        {
            0 => DefaultCurrency.PLN,
            1 => DefaultCurrency.USD,
            _ => null
        });
    }
}