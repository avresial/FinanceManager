using FinanceManager.Domain.FinancialAccounts.Investments.Entities;
using FinanceManager.Domain.FinancialAccounts.Investments.Repositories;
using FinanceManager.Infrastructure.Contexts;
using Microsoft.EntityFrameworkCore;

namespace FinanceManager.Infrastructure.Repositories.Investments;

public class MarketDataSymbolRepository(AppDbContext context) : IMarketDataSymbolRepository
{
    public Task<MarketDataSymbol?> Get(long id, CancellationToken cancellationToken = default) =>
        context.MarketDataSymbols.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public async Task<IReadOnlyList<MarketDataSymbol>> GetByListing(long assetListingId, CancellationToken cancellationToken = default) =>
        await context.MarketDataSymbols.AsNoTracking()
            .Where(x => x.AssetListingId == assetListingId)
            .ToListAsync(cancellationToken);

    public Task<MarketDataSymbol?> GetPrimary(long assetListingId, MarketDataProvider provider, CancellationToken cancellationToken = default) =>
        context.MarketDataSymbols.AsNoTracking()
            .Where(x => x.AssetListingId == assetListingId && x.Provider == provider && x.IsEnabled)
            .OrderByDescending(x => x.IsPrimary)
            .ThenBy(x => x.Id)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<MarketDataSymbol> Add(MarketDataSymbol symbol, CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        if (symbol.CreatedAt == default) symbol.CreatedAt = now;
        symbol.UpdatedAt = now;

        context.MarketDataSymbols.Add(symbol);
        await context.SaveChangesAsync(cancellationToken);
        return symbol;
    }

    public async Task<MarketDataSymbol> Upsert(MarketDataSymbol symbol, CancellationToken cancellationToken = default)
    {
        var existing = await context.MarketDataSymbols.FirstOrDefaultAsync(
            x => x.Provider == symbol.Provider && x.Symbol == symbol.Symbol, cancellationToken);

        if (existing is not null)
        {
            existing.AssetListingId = symbol.AssetListingId;
            existing.ProviderExchangeCode = symbol.ProviderExchangeCode;
            existing.ProviderInstrumentId = symbol.ProviderInstrumentId;
            existing.Currency = symbol.Currency;
            existing.IsPrimary = symbol.IsPrimary;
            existing.IsEnabled = symbol.IsEnabled;
            existing.UpdatedAt = DateTimeOffset.UtcNow;
            await context.SaveChangesAsync(cancellationToken);
            return existing;
        }

        return await Add(symbol, cancellationToken);
    }

    public async Task RecordFetchResult(long id, DateTimeOffset? lastSuccessfulPriceFetchAt, string? lastError, CancellationToken cancellationToken = default)
    {
        var symbol = await context.MarketDataSymbols.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (symbol is null) return;

        if (lastSuccessfulPriceFetchAt is DateTimeOffset success)
        {
            symbol.LastSuccessfulPriceFetchAt = success;
            symbol.LastValidatedAt = success;
        }
        symbol.LastError = lastError;
        symbol.UpdatedAt = DateTimeOffset.UtcNow;
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> Delete(long id, CancellationToken cancellationToken = default)
    {
        var entity = await context.MarketDataSymbols.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (entity is null) return false;

        context.MarketDataSymbols.Remove(entity);
        await context.SaveChangesAsync(cancellationToken);
        return true;
    }
}