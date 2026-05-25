using FinanceManager.Domain.Dtos;
using FinanceManager.Domain.Entities.Currencies;
using FinanceManager.Domain.Entities.Stocks;
using FinanceManager.Domain.Repositories;
using FinanceManager.Domain.Services;

namespace FinanceManager.Application.Services.Stocks;

internal class StockMarketService(
    IAlphaVantageClient apiClient,
    IStockPriceProvider stockPriceProvider,
    ICurrencyRepository currencyRepository) : IStockMarketService
{
    public Task<IReadOnlyList<TickerSearchMatch>> SearchTicker(string keywords, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(keywords)) return Task.FromResult<IReadOnlyList<TickerSearchMatch>>([]);
        return apiClient.SearchTicker(keywords, ct);
    }

    public Task<IReadOnlyList<StockPrice>> GetStockPrices(string ticker, DateTime start, DateTime end, CancellationToken ct = default)
        => stockPriceProvider.GetPricesAsync(ticker, start, end, ct);

    public async Task<IReadOnlyList<StockDetails>> ListStockDetails(CancellationToken ct = default)
    {
        var listings = await apiClient.GetListings(ct);
        if (listings.Count == 0) return [];

        var defaultCurrency = await currencyRepository.GetOrAdd(DefaultCurrency.USD.ShortName, DefaultCurrency.USD.Symbol, ct);
        var stockDetailsList = new List<StockDetails>(listings.Count);

        foreach (var listing in listings)
        {
            if (string.IsNullOrWhiteSpace(listing.Symbol)) continue;

            stockDetailsList.Add(new StockDetails
            {
                Ticker = listing.Symbol,
                Name = listing.Name ?? string.Empty,
                Type = listing.AssetType ?? string.Empty,
                Region = listing.Exchange ?? string.Empty,
                Currency = defaultCurrency
            });
        }

        return stockDetailsList;
    }
}