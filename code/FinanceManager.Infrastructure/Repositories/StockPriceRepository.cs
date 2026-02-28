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
        var stockDetails = await GetOrCreateStockDetails(ticker, currency);
        StockPriceDto stockPriceDto = new(
            0, // Id is autoincremented, will be set by the database
            stockDetails,
            pricePerUnit,
            date.Date
        );

        var entry = await context.StockPrices.AddAsync(stockPriceDto);
        await context.SaveChangesAsync();

        return entry.Entity.ToStockPrice();
    }

    public async Task<DateTime?> GetLatestMissing(string ticker)
    {
        var normalizedTicker = ticker.Trim().ToUpperInvariant();
        var searchDate = DateTime.UtcNow.Date;
        var stockPrice = await context.StockPrices
            .Include(x => x.StockDetails)
            .FirstOrDefaultAsync(x => EF.Property<string>(x, "StockTicker") == normalizedTicker && x.Date.Date == searchDate.Date);
        if (stockPrice is null) return searchDate;

        int tries = 365 * 99;
        do
        {
            searchDate = searchDate.AddDays(-1);
            stockPrice = await context.StockPrices
                .Include(x => x.StockDetails)
                .FirstOrDefaultAsync(x => EF.Property<string>(x, "StockTicker") == normalizedTicker && x.Date.Date == searchDate.Date);
            if (stockPrice is null) return searchDate;

        } while (stockPrice is not null && --tries > 0);

        return null;
    }

    public async Task<StockPrice?> Get(string ticker, DateTime date)
    {
        var normalizedTicker = ticker.Trim().ToUpperInvariant();
        var stockPrice = await context.StockPrices
            .Include(x => x.StockDetails)
            .ThenInclude(x => x.Currency)
            .FirstOrDefaultAsync(x => EF.Property<string>(x, "StockTicker") == normalizedTicker && x.Date.Date == date.Date);
        if (stockPrice is null || stockPrice.StockDetails is null) return null;

        return stockPrice.ToStockPrice();
    }

    public async Task<IReadOnlyList<StockPrice>> GetRange(string ticker, DateTime start, DateTime end)
    {
        var normalizedTicker = ticker.Trim().ToUpperInvariant();
        return await context.StockPrices
            .Include(x => x.StockDetails)
            .ThenInclude(x => x.Currency)
            .Where(x => EF.Property<string>(x, "StockTicker") == normalizedTicker && x.Date.Date >= start.Date && x.Date.Date <= end.Date)
            .OrderByDescending(x => x.Date)
            .Select(x => x.ToStockPrice())
            .ToListAsync();
    }
    public async Task<StockPrice?> GetThisOrNextOlder(string ticker, DateTime date)
    {
        var result = await Get(ticker, date);
        if (result is not null) return result;

        var normalizedTicker = ticker.Trim().ToUpperInvariant();
        return await context.StockPrices
            .Include(x => x.StockDetails)
            .ThenInclude(x => x.Currency)
            .Where(x => EF.Property<string>(x, "StockTicker") == normalizedTicker && x.Date < date)
            .OrderByDescending(x => x.Date)
            .Select(x => x.ToStockPrice())
            .FirstOrDefaultAsync();
    }

    public async Task<Currency?> GetTickerCurrency(string ticker)
    {
        var stockDetails = await context.StockDetails
            .Include(x => x.Currency)
            .FirstOrDefaultAsync(x => x.Ticker == ticker);

        return stockDetails?.Currency;
    }

    public async Task Add(IEnumerable<StockPrice> prices)
    {
        foreach (var price in prices)
        {
            if (context.Entry(price.Currency).State == EntityState.Detached)
                context.Attach(price.Currency);

            var normalizedTicker = price.Ticker.Trim().ToUpperInvariant();
            var stockDetails = await GetOrCreateStockDetails(normalizedTicker, price.Currency);

            var date = price.Date.Date;
            var existing = await context.StockPrices
                .Include(x => x.StockDetails)
                .FirstOrDefaultAsync(x => EF.Property<string>(x, "StockTicker") == normalizedTicker && x.Date.Date == date);
            if (existing is null)
            {
                var dto = new StockPriceDto(0, stockDetails, price.PricePerUnit, date);
                await context.StockPrices.AddAsync(dto);
            }
            else
            {
                existing.PricePerUnit = price.PricePerUnit;
                existing.StockDetails = stockDetails;
            }
        }

        await context.SaveChangesAsync();
    }

    public async Task<bool> Delete(int id, CancellationToken ct = default)
    {
        var entity = await context.StockPrices.FindAsync(new object[] { id }, ct);
        if (entity is null) return false;

        context.StockPrices.Remove(entity);
        await context.SaveChangesAsync(ct);
        return true;
    }

    public async Task<StockPrice> Update(string ticker, decimal pricePerUnit, Currency currency, DateTime date)
    {
        var normalizedTicker = ticker.Trim().ToUpperInvariant();
        var stockPrice = await context.StockPrices
            .Include(x => x.StockDetails)
            .FirstOrDefaultAsync(x => EF.Property<string>(x, "StockTicker") == normalizedTicker && x.Date.Date == date.Date)
            ?? throw new Exception("Update failed");

        var stockDetails = await GetOrCreateStockDetails(normalizedTicker, currency);

        stockPrice.PricePerUnit = pricePerUnit;
        stockPrice.StockDetails = stockDetails;

        await context.SaveChangesAsync();

        return stockPrice.ToStockPrice();
    }

    private async Task<StockDetails> GetOrCreateStockDetails(string ticker, Currency currency)
    {
        var normalized = ticker.Trim().ToUpperInvariant();
        var existing = await context.StockDetails.Include(x => x.Currency).FirstOrDefaultAsync(x => x.Ticker == normalized);
        if (existing is not null)
        {
            if (existing.Currency.Id != currency.Id)
                existing.Currency = currency;
            return existing;
        }

        var created = new StockDetails
        {
            Ticker = normalized,
            Currency = currency,
            Name = string.Empty,
            Type = string.Empty,
            Region = string.Empty
        };

        context.StockDetails.Add(created);
        await context.SaveChangesAsync();
        return created;
    }
}