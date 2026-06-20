using FinanceManager.Domain.Assets.Entities;
using FinanceManager.Domain.Assets.Repositories;
using FinanceManager.Infrastructure.Contexts;
using Microsoft.EntityFrameworkCore;

namespace FinanceManager.Infrastructure.Repositories.Assets;

public class PriceQuoteRepository(AppDbContext context) : IPriceQuoteRepository
{
    public Task<PriceQuote?> Get(long id, CancellationToken cancellationToken = default) =>
        context.PriceQuotes.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public Task<PriceQuote?> GetLatestOnOrBefore(long assetListingId, DateTimeOffset asOf, MarketDataProvider? provider = null, CancellationToken cancellationToken = default)
    {
        var query = context.PriceQuotes.AsNoTracking()
            .Where(x => x.AssetListingId == assetListingId && x.PriceTime <= asOf);

        if (provider is MarketDataProvider p)
            query = query.Where(x => x.Provider == p);

        return query.OrderByDescending(x => x.PriceTime).FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<PriceQuote>> GetRange(long assetListingId, DateTimeOffset start, DateTimeOffset end, CancellationToken cancellationToken = default) =>
        await context.PriceQuotes.AsNoTracking()
            .Where(x => x.AssetListingId == assetListingId && x.PriceTime >= start && x.PriceTime <= end)
            .OrderBy(x => x.PriceTime)
            .ToListAsync(cancellationToken);

    public async Task<PriceQuote> Add(PriceQuote quote, CancellationToken cancellationToken = default)
    {
        if (quote.FetchedAt == default) quote.FetchedAt = DateTimeOffset.UtcNow;

        context.PriceQuotes.Add(quote);
        await context.SaveChangesAsync(cancellationToken);
        return quote;
    }

    public async Task<PriceQuote> Upsert(PriceQuote quote, CancellationToken cancellationToken = default)
    {
        var existing = await context.PriceQuotes.FirstOrDefaultAsync(
            x => x.AssetListingId == quote.AssetListingId
                && x.Provider == quote.Provider
                && x.PriceTime == quote.PriceTime
                && x.QuoteType == quote.QuoteType,
            cancellationToken);

        if (existing is not null)
        {
            existing.MarketDataSymbolId = quote.MarketDataSymbolId;
            existing.Price = quote.Price;
            existing.Currency = quote.Currency;
            existing.RawPrice = quote.RawPrice;
            existing.RawCurrency = quote.RawCurrency;
            existing.FetchedAt = quote.FetchedAt == default ? DateTimeOffset.UtcNow : quote.FetchedAt;
            await context.SaveChangesAsync(cancellationToken);
            return existing;
        }

        return await Add(quote, cancellationToken);
    }

    public async Task<bool> Delete(long id, CancellationToken cancellationToken = default)
    {
        var entity = await context.PriceQuotes.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (entity is null) return false;

        context.PriceQuotes.Remove(entity);
        await context.SaveChangesAsync(cancellationToken);
        return true;
    }
}