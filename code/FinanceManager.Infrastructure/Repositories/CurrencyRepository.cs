using FinanceManager.Domain.Entities.Currencies;
using FinanceManager.Domain.Repositories;
using FinanceManager.Infrastructure.Contexts;
using Microsoft.EntityFrameworkCore;
using System.Runtime.CompilerServices;

namespace FinanceManager.Infrastructure.Repositories;

public class CurrencyRepository(AppDbContext context) : ICurrencyRepository
{
    public async IAsyncEnumerable<Currency> GetCurrencies([EnumeratorCancellation] CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        await EnsureDefaults(ct);

        await foreach (var currency in context.Currencies.AsAsyncEnumerable().WithCancellation(ct))
        {
            yield return currency;
        }
    }

    public async Task<Currency?> GetCurrency(int id, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        await EnsureDefaults(ct);

        return await context.Currencies.FirstOrDefaultAsync(x => x.Id == id, ct);
    }

    public async Task<Currency?> GetByCode(string shortName, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        await EnsureDefaults(ct);

        var normalized = shortName.Trim().ToUpperInvariant();
        return await context.Currencies.FirstOrDefaultAsync(x => x.ShortName == normalized, ct);
    }

    public async Task<Currency> GetOrAdd(string shortName, string? symbol, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        await EnsureDefaults(ct);

        var normalized = shortName.Trim().ToUpperInvariant();
        var existing = await context.Currencies.FirstOrDefaultAsync(x => x.ShortName == normalized, ct);
        if (existing is not null) return existing;

        var nextId = await GetNextId(ct);
        var currency = new Currency(nextId, normalized, string.IsNullOrWhiteSpace(symbol) ? normalized : symbol);
        context.Currencies.Add(currency);
        await context.SaveChangesAsync(ct);
        return currency;
    }

    private async Task EnsureDefaults(CancellationToken ct)
    {
        if (await context.Currencies.AnyAsync(ct)) return;

        context.Currencies.AddRange(DefaultCurrency.PLN, DefaultCurrency.USD);
        await context.SaveChangesAsync(ct);
    }

    private async Task<int> GetNextId(CancellationToken ct)
    {
        var maxId = await context.Currencies.MaxAsync(x => (int?)x.Id, ct) ?? 1;
        return maxId + 1;
    }
}