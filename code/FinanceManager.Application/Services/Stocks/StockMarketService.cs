using FinanceManager.Domain.Dtos;
using FinanceManager.Domain.Entities.Currencies;
using FinanceManager.Domain.Entities.Stocks;
using FinanceManager.Domain.Repositories;

namespace FinanceManager.Application.Services.Stocks;

internal class StockMarketService(
    IAlphaVantageClient apiClient,
    IStockPriceRepository stockPriceRepository,
    ICurrencyRepository currencyRepository,
    IStockDetailsRepository stockDetailsRepository) : IStockMarketService
{
    public Task<IReadOnlyList<TickerSearchMatch>> SearchTicker(string keywords, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(keywords)) return Task.FromResult<IReadOnlyList<TickerSearchMatch>>([]);
        return apiClient.SearchTicker(keywords, ct);
    }

    public async Task<IReadOnlyList<StockPrice>> GetStockPrices(string ticker, DateTime start, DateTime end, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(ticker) || start == default || end == default) return [];
        if (end < start) return [];

        var normalizedTicker = ticker.Trim().ToUpperInvariant();
        IReadOnlyList<StockPrice> existing = [];
        if (start.Date == end.Date)
        {
            var price = await stockPriceRepository.Get(normalizedTicker, start);
            if (price is not null)
                return [price];
        }
        else
        {
            existing = await stockPriceRepository.GetRange(normalizedTicker, start, end);
        }

        if (NeedsFetch(existing, start, end))
        {
            var stockDetails = await ResolveStockDetails(normalizedTicker, ct);
            var apiPrices = await apiClient.GetDailySeries(normalizedTicker, start, end, stockDetails.Currency, ct);
            if (apiPrices.Count > 0)
            {
                await stockPriceRepository.Add(apiPrices);
                existing = MergeByDate(existing, apiPrices);
            }
        }

        return existing.Where(x => x.Date >= start && x.Date <= end).OrderByDescending(x => x.Date).ToList();
    }

    public async Task<IReadOnlyList<StockDetails>> ListStockDetails(CancellationToken ct = default)
    {
        var listings = await apiClient.GetListings(ct);
        if (listings.Count == 0) return [];

        var defaultCurrency = await currencyRepository.GetOrAdd(DefaultCurrency.USD.ShortName, DefaultCurrency.USD.Symbol, ct);
        var stockDetailsList = new List<StockDetails>(listings.Count);

        foreach (var listing in listings)
        {
            if (string.IsNullOrWhiteSpace(listing.Symbol)) continue;

            var stockDetails = new StockDetails
            {
                Ticker = listing.Symbol,
                Name = listing.Name ?? string.Empty,
                Type = listing.AssetType ?? string.Empty,
                Region = listing.Exchange ?? string.Empty,
                Currency = defaultCurrency
            };

            stockDetailsList.Add(stockDetails);
        }

        return stockDetailsList;
    }

    private async Task<StockDetails> ResolveStockDetails(string ticker, CancellationToken ct)
    {
        var existingDetails = await stockDetailsRepository.Get(ticker, ct);
        if (existingDetails is not null && !NeedsEnrichment(existingDetails))
            return existingDetails;

        var matches = await SearchTicker(ticker, ct);
        var exact = matches.FirstOrDefault(x => string.Equals(x.Symbol, ticker, StringComparison.OrdinalIgnoreCase));
        var selected = exact ?? matches.FirstOrDefault();

        Currency currency;
        if (selected is not null && !string.IsNullOrWhiteSpace(selected.Currency))
            currency = await currencyRepository.GetOrAdd(selected.Currency, selected.Currency, ct);
        else
            currency = existingDetails?.Currency ?? await currencyRepository.GetOrAdd(DefaultCurrency.USD.ShortName, DefaultCurrency.USD.Symbol, ct);

        var details = new StockDetails
        {
            Ticker = ticker,
            Name = selected?.Name ?? existingDetails?.Name ?? string.Empty,
            Type = selected?.Type ?? existingDetails?.Type ?? string.Empty,
            Region = selected?.Region ?? existingDetails?.Region ?? string.Empty,
            Currency = currency
        };

        return await stockDetailsRepository.Add(details, ct);
    }

    private static bool NeedsEnrichment(StockDetails details)
        => string.IsNullOrWhiteSpace(details.Name)
           || string.IsNullOrWhiteSpace(details.Type)
           || string.IsNullOrWhiteSpace(details.Region);

    private static bool NeedsFetch(IReadOnlyList<StockPrice> existing, DateTime start, DateTime end)
    {
        if (existing.Count == 0) return true;
        if (existing.Count == 1 && start.Date == end.Date) return false;

        var existingDates = existing.Select(x => x.Date.Date).ToHashSet();
        for (var date = start.Date; date <= end.Date; date = date.AddDays(1))
        {
            if (date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday) continue;
            if (!existingDates.Contains(date)) return true;
        }

        return false;
    }

    private static IReadOnlyList<StockPrice> MergeByDate(IReadOnlyList<StockPrice> existing, IReadOnlyList<StockPrice> fetched)
    {
        var merged = existing.Concat(fetched)
            .GroupBy(x => x.Date.Date)
            .Select(x => x.OrderByDescending(p => p.Date).First())
            .ToList();

        return merged;
    }
}