using FinanceManager.Domain.Assets.Entities;
using FinanceManager.Domain.Assets.Repositories;
using FinanceManager.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FinanceManager.Infrastructure.Features.Assets.Repositories;

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

    public async Task<IReadOnlyList<PriceQuote>> UpsertRange(IReadOnlyList<PriceQuote> quotes, CancellationToken cancellationToken = default)
    {
        if (quotes.Count == 0) return [];

        var listingIds = quotes.Select(q => q.AssetListingId).Distinct().ToList();
        var providers = quotes.Select(q => q.Provider).Distinct().ToList();
        var quoteTypes = quotes.Select(q => q.QuoteType).Distinct().ToList();
        var minPriceTime = quotes.Min(q => q.PriceTime);
        var maxPriceTime = quotes.Max(q => q.PriceTime);

        var existing = await context.PriceQuotes
            .Where(x => listingIds.Contains(x.AssetListingId)
                && providers.Contains(x.Provider)
                && quoteTypes.Contains(x.QuoteType)
                && x.PriceTime >= minPriceTime
                && x.PriceTime <= maxPriceTime)
            .ToListAsync(cancellationToken);

        var existingByKey = existing.ToDictionary(
            x => (x.AssetListingId, x.Provider, x.PriceTime, x.QuoteType));

        var result = new List<PriceQuote>(quotes.Count);
        foreach (var quote in quotes)
        {
            var key = (quote.AssetListingId, quote.Provider, quote.PriceTime, quote.QuoteType);
            if (existingByKey.TryGetValue(key, out var existingQuote))
            {
                existingQuote.MarketDataSymbolId = quote.MarketDataSymbolId;
                existingQuote.Price = quote.Price;
                existingQuote.Currency = quote.Currency;
                existingQuote.RawPrice = quote.RawPrice;
                existingQuote.RawCurrency = quote.RawCurrency;
                existingQuote.FetchedAt = quote.FetchedAt == default ? DateTimeOffset.UtcNow : quote.FetchedAt;
                result.Add(existingQuote);
            }
            else
            {
                if (quote.FetchedAt == default) quote.FetchedAt = DateTimeOffset.UtcNow;
                context.PriceQuotes.Add(quote);
                result.Add(quote);
            }
        }

        await context.SaveChangesAsync(cancellationToken);
        return result;
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