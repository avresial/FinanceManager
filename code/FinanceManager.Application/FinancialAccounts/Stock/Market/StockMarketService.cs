using FinanceManager.Application.FinancialAccounts.Stock.Pricing;
using FinanceManager.Domain.Dtos;
using FinanceManager.Domain.Entities.Currencies;
using FinanceManager.Domain.FinancialAccounts.Stock.Dtos;
using FinanceManager.Domain.FinancialAccounts.Stock.Entities;
using FinanceManager.Domain.FinancialAccounts.Stock.Services;
using FinanceManager.Domain.Identity.Repositories;
using FinanceManager.Domain.Identity.Services;
using FinanceManager.Domain.Repositories;
using FinanceManager.Domain.Services;

namespace FinanceManager.Application.FinancialAccounts.Stock.Market;

internal class StockMarketService(
    IAlphaVantageClient apiClient,
    IStockPriceProvider stockPriceProvider,
    ICurrencyRepository currencyRepository,
    IIsinResolver isinResolver) : IStockMarketService
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

            var isin = await isinResolver.ResolveAsync(listing.Symbol, ct: ct);
            if (isin is null) continue;

            stockDetailsList.Add(new StockDetails
            {
                Isin = isin,
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