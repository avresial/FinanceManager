using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.FinancialAccounts.Currencies.Repositories;
using FinanceManager.Domain.Identity.Repositories;
using FinanceManager.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System.Runtime.CompilerServices;

namespace FinanceManager.Infrastructure.Features.FinancialAccounts.Currencies.Repositories;

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
        var currency = await context.Currencies.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (currency is not null) return currency;

        await EnsureDefaults(ct);
        return await context.Currencies.FirstOrDefaultAsync(x => x.Id == id, ct);
    }

    public async Task<Currency?> GetByCode(string shortName, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var normalized = shortName.Trim().ToUpperInvariant();
        var currency = await context.Currencies.FirstOrDefaultAsync(x => x.ShortName == normalized, ct);
        if (currency is not null) return currency;

        await EnsureDefaults(ct);
        return await context.Currencies.FirstOrDefaultAsync(x => x.ShortName == normalized, ct);
    }

    public async Task<Currency> GetOrAdd(string shortName, string? symbol, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var normalized = shortName.Trim().ToUpperInvariant();
        var existing = await context.Currencies.FirstOrDefaultAsync(x => x.ShortName == normalized, ct);
        if (existing is not null) return existing;

        await EnsureDefaults(ct);
        existing = await context.Currencies.FirstOrDefaultAsync(x => x.ShortName == normalized, ct);
        if (existing is not null) return existing;

        var nextId = await GetNextId(ct);
        var currency = new Currency(nextId, normalized, string.IsNullOrWhiteSpace(symbol) ? normalized : symbol);
        context.Currencies.Add(currency);
        await context.SaveChangesAsync(ct);
        return currency;
    }

    private async Task EnsureDefaults(CancellationToken ct)
    {
        var existingCodes = await context.Currencies
            .Select(x => x.ShortName)
            .ToListAsync(ct);

        var missingDefaults = new[] { DefaultCurrency.PLN, DefaultCurrency.USD }
            .Where(x => !existingCodes.Contains(x.ShortName, StringComparer.OrdinalIgnoreCase))
            .ToList();

        var missingCommon = DefaultCurrency.CommonCurrencies
            .Where(x => !existingCodes.Contains(x.ShortName, StringComparer.OrdinalIgnoreCase))
            .ToList();

        if (missingDefaults.Count == 0 && missingCommon.Count == 0) return;

        context.Currencies.AddRange(missingDefaults);

        // Common currencies get ids after every already-stored currency, so they never collide
        // with ids handed out earlier by GetOrAdd.
        var nextId = Math.Max(await context.Currencies.MaxAsync(x => (int?)x.Id, ct) ?? 1, 1) + 1;
        foreach (var (shortName, symbol) in missingCommon)
            context.Currencies.Add(new Currency(nextId++, shortName, symbol));

        await context.SaveChangesAsync(ct);
    }

    private async Task<int> GetNextId(CancellationToken ct)
    {
        var maxId = await context.Currencies.MaxAsync(x => (int?)x.Id, ct) ?? 1;
        return maxId + 1;
    }
}