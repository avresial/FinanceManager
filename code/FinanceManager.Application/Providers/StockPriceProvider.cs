using FinanceManager.Domain.Entities;
using FinanceManager.Domain.Repositories;
using FinanceManager.Domain.Services;
using Microsoft.Extensions.Caching.Memory;

namespace FinanceManager.Application.Providers;

public class StockPriceProvider(IStockPriceRepository stockRepository, ICurrencyExchangeService currencyExchangeService, IMemoryCache cache) : IStockPriceProvider
{
    public Task<decimal> GetPricePerUnitAsync(string ticker, Currency targetCurrency, DateTime asOf)
    {
        if (string.IsNullOrWhiteSpace(ticker)) throw new ArgumentException("{ticker}", nameof(ticker));

        var key = ticker.Trim().ToUpperInvariant();

        if (cache.TryGetValue(key, out decimal cached))
            return Task.FromResult(cached);

        return cache.GetOrCreateAsync(key, async entry =>
        {
            entry.SlidingExpiration = TimeSpan.FromMinutes(5);

            var stockPrice = await stockRepository.GetThisOrNextOlder(ticker, asOf);
            decimal price = 1m;
            if (stockPrice is not null)
                price = await currencyExchangeService.GetPricePerUnit(stockPrice, targetCurrency, asOf);

            return price;
        });
    }
}
