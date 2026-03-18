using FinanceManager.Domain.Entities.Currencies;
using FinanceManager.Domain.Repositories;
using FinanceManager.Domain.Services;
using Microsoft.Extensions.Caching.Memory;

namespace FinanceManager.Application.Providers;

public class StockPriceProvider(IStockPriceRepository stockRepository, ICurrencyExchangeService currencyExchangeService, IMemoryCache cache) : IStockPriceProvider
{
    public async Task<decimal> GetPricePerUnitAsync(string ticker, Currency targetCurrency, DateTime asOf)
    {
        if (string.IsNullOrWhiteSpace(ticker)) throw new ArgumentException("{ticker}", nameof(ticker));

        var key = $"STOCK_PRICE_{targetCurrency.ShortName}_{asOf:yyyyMMdd}_{ticker.Trim().ToUpperInvariant()}";

        if (cache.TryGetValue(key, out decimal cached))
            return cached;

        var stockPrice = await stockRepository.GetThisOrNextOlder(ticker, asOf);
        decimal price = 0m;
        if (stockPrice is not null)
        {
            if (stockPrice.Currency == targetCurrency)
                price = stockPrice.PricePerUnit;
            else
                price = await currencyExchangeService.GetPricePerUnit(stockPrice, targetCurrency, asOf);
        }

        if (price > 0)
            cache.Set(key, price, new MemoryCacheEntryOptions { SlidingExpiration = TimeSpan.FromMinutes(60) });

        return price;
    }
}