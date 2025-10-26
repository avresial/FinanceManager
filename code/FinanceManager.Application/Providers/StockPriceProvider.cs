using FinanceManager.Domain.Entities;
using FinanceManager.Domain.Repositories;
using FinanceManager.Domain.Services;
using Microsoft.Extensions.Caching.Memory;

namespace FinanceManager.Application.Providers;

public class StockPriceProvider(IStockPriceRepository stockRepository, ICurrencyExchangeService currencyExchangeService, IMemoryCache cache) : IStockPriceProvider
{
    private readonly IStockPriceRepository _stockRepository = stockRepository ?? throw new ArgumentNullException(nameof(stockRepository));
    private readonly ICurrencyExchangeService _currencyExchangeService = currencyExchangeService ?? throw new ArgumentNullException(nameof(currencyExchangeService));
    private readonly IMemoryCache _cache = cache ?? throw new ArgumentNullException(nameof(cache));

    public async Task<decimal> GetPricePerUnitAsync(string ticker, Currency targetCurrency, DateTime asOf)
    {
        if (string.IsNullOrWhiteSpace(ticker)) throw new ArgumentException("{ticker}", nameof(ticker));

        var key = ticker.Trim().ToUpperInvariant();

        if (_cache.TryGetValue(key, out decimal cached))
            return cached;

        var pricePerUnit = await _cache.GetOrCreateAsync(key, async entry =>
        {
            entry.SlidingExpiration = TimeSpan.FromMinutes(5);

            var stockPrice = await _stockRepository.GetThisOrNextOlder(ticker, asOf);
            decimal price = 1m;
            if (stockPrice is not null)
                price = await _currencyExchangeService.GetPricePerUnit(stockPrice, targetCurrency, asOf);

            return price;
        });

        return pricePerUnit;
    }
}
