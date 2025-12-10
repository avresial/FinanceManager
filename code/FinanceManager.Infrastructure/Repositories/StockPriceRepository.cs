using FinanceManager.Domain.Entities.Currencies;
using FinanceManager.Domain.Entities.Stocks;
using FinanceManager.Domain.Repositories;
using FinanceManager.Infrastructure.Contexts;
using FinanceManager.Infrastructure.Dtos;
using Microsoft.EntityFrameworkCore;

namespace FinanceManager.Infrastructure.Repositories;

public class StockPriceRepository(AppDbContext context) : IStockPriceRepository
{
    public async Task<StockPrice> Add(string ticker, decimal pricePerUnit, Currency currency, DateTime date)
    {
        StockPriceDto stockPriceDto = new(
            0, // Id is autoincremented, will be set by the database
            ticker,
            pricePerUnit,
            currency,
            date.Date
        );

        var entry = await context.StockPrices.AddAsync(stockPriceDto);
        await context.SaveChangesAsync();

        return entry.Entity.ToStockPrice();
    }

    public async Task<DateTime?> GetLatestMissing(string ticker)
    {
        var searchDate = DateTime.UtcNow.Date;
        var stockPrice = await context.StockPrices.FirstOrDefaultAsync(x => x.Ticker == ticker && x.Date.Date == searchDate.Date);
        if (stockPrice is null) return searchDate;

        int tries = 365 * 99;
        do
        {
            searchDate = searchDate.AddDays(-1);
            stockPrice = await context.StockPrices.FirstOrDefaultAsync(x => x.Ticker == ticker && x.Date.Date == searchDate.Date);
            if (stockPrice is null) return searchDate;

        } while (stockPrice is not null && --tries > 0);

        return null;
    }

    public async Task<StockPrice?> Get(string ticker, DateTime date)
    {
        var stockPrice = await context.StockPrices.FirstOrDefaultAsync(x => x.Ticker == ticker && x.Date.Date == date.Date);
        if (stockPrice is null) return null;

        return stockPrice.ToStockPrice();
    }
    public async Task<StockPrice?> GetThisOrNextOlder(string ticker, DateTime date)
    {
        var result = await Get(ticker, date);
        if (result is not null) return result;

        return await context.StockPrices
            .Where(x => x.Ticker == ticker && x.Date < date)
            .OrderByDescending(x => x.Date)
            .Select(x => x.ToStockPrice())
            .FirstOrDefaultAsync();
    }

    public async Task<Currency?> GetTickerCurrency(string ticker)
    {
        var stockPrice = await context.StockPrices.FirstOrDefaultAsync(x => x.Ticker == ticker);

        if (stockPrice is null) return null;
        return stockPrice.Currency;
    }

    public async Task<StockPrice> Update(string ticker, decimal pricePerUnit, Currency currency, DateTime date)
    {
        var stockPrice = await context.StockPrices.FirstOrDefaultAsync(x => x.Ticker == ticker && x.Date.Date == date.Date) ?? throw new Exception("Update failed");

        stockPrice.PricePerUnit = pricePerUnit;
        stockPrice.Currency = currency;

        await context.SaveChangesAsync();

        return stockPrice.ToStockPrice();
    }
}