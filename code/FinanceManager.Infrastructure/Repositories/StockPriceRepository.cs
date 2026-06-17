using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.FinancialAccounts.Stock.Entities;
using FinanceManager.Domain.FinancialAccounts.Stock.Repositories;
using FinanceManager.Domain.Identity.Repositories;
using FinanceManager.Infrastructure.Contexts;
using FinanceManager.Infrastructure.Dtos;
using Microsoft.EntityFrameworkCore;

namespace FinanceManager.Infrastructure.Repositories;

public class StockPriceRepository(AppDbContext context) : IStockPriceRepository
{
    public async Task<StockPrice> Add(string isin, decimal pricePerUnit, Currency currency, DateTime date)
    {
        var stockDetails = await GetOrCreateStockDetails(isin, currency);

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

    public async Task<DateTime?> GetLatestMissing(string isin)
    {
        var today = DateTime.UtcNow.Date;
        var twoYearsAgo = today.AddYears(-2);

        var stockPrices = await context.StockPrices
            .AsNoTracking()
            .Where(x => x.StockDetails!.Isin == isin && x.Date >= twoYearsAgo)
            .Select(x => x.Date.Date)
            .Distinct()
            .OrderByDescending(x => x)
            .ToListAsync();

        if (stockPrices.Count == 0 || stockPrices[0] < today)
            return today;

        for (int i = 0; i < stockPrices.Count - 1; i++)
        {
            var currentDate = stockPrices[i];
            var nextDate = stockPrices[i + 1];
            var expectedNextDate = currentDate.AddDays(-1);

            if (nextDate < expectedNextDate)
                return expectedNextDate;
        }

        return null;
    }

    public async Task<StockPrice?> Get(string isin, DateTime date)
    {
        var stockPrice = await context.StockPrices
            .AsNoTracking()
            .Include(x => x.StockDetails)
            .ThenInclude(x => x.Currency)
            .FirstOrDefaultAsync(x => x.StockDetails!.Isin == isin && x.Date == date.Date);
        if (stockPrice is null || stockPrice.StockDetails is null) return null;

        return stockPrice.ToStockPrice();
    }

    public async Task<IReadOnlyList<StockPrice>> GetRange(string isin, DateTime start, DateTime end)
    {
        return await context.StockPrices
            .AsNoTracking()
            .Include(x => x.StockDetails)
            .ThenInclude(x => x.Currency)
            .Where(x => x.StockDetails!.Isin == isin && x.Date >= start.Date && x.Date < end.Date.AddDays(1))
            .OrderByDescending(x => x.Date)
            .Select(x => x.ToStockPrice())
            .ToListAsync();
    }

    public async Task<StockPrice?> GetThisOrNextOlder(string isin, DateTime date)
    {
        var result = await Get(isin, date);
        if (result is not null) return result;

        return await context.StockPrices
            .AsNoTracking()
            .Include(x => x.StockDetails)
            .ThenInclude(x => x.Currency)
            .Where(x => x.StockDetails!.Isin == isin && x.Date < date)
            .OrderByDescending(x => x.Date)
            .Select(x => x.ToStockPrice())
            .FirstOrDefaultAsync();
    }

    public async Task<Currency?> GetStockCurrency(string isin)
    {
        var stockDetails = await context.StockDetails
            .AsNoTracking()
            .Include(x => x.Currency)
            .FirstOrDefaultAsync(x => x.Isin == isin);

        return stockDetails?.Currency;
    }

    public async Task Add(IEnumerable<StockPrice> prices)
    {
        var priceList = prices.ToList();
        if (priceList.Count == 0) return;

        var isins = priceList.Select(p => p.Isin).Distinct().ToList();

        var stockDetailsByIsin = await context.StockDetails
            .Where(sd => isins.Contains(sd.Isin))
            .ToDictionaryAsync(sd => sd.Isin);

        var minDate = priceList.Min(p => p.Date.Date);
        var maxDate = priceList.Max(p => p.Date.Date);
        var dayAfterMax = maxDate.AddDays(1);

        var existingPrices = await context.StockPrices
            .Include(x => x.StockDetails)
            .Where(x => isins.Contains(x.StockDetails!.Isin) && x.Date >= minDate && x.Date < dayAfterMax)
            .ToListAsync();

        var existingByKey = existingPrices.ToDictionary(x => (x.StockDetails!.Isin, x.Date.Date));

        var attachedCurrencyIds = new HashSet<int>();
        var toAdd = new List<StockPriceDto>();

        foreach (var price in priceList)
        {
            if (!stockDetailsByIsin.TryGetValue(price.Isin, out var stockDetails)) continue;

            if (attachedCurrencyIds.Add(price.Currency.Id) && context.Entry(price.Currency).State == EntityState.Detached)
                context.Attach(price.Currency);

            if (stockDetails.Currency.Id != price.Currency.Id)
                stockDetails.Currency = price.Currency;

            var date = price.Date.Date;

            if (existingByKey.TryGetValue((price.Isin, date), out var existing))
            {
                existing.PricePerUnit = price.PricePerUnit;
                existing.StockDetails = stockDetails;
            }
            else
            {
                toAdd.Add(new StockPriceDto(0, stockDetails, price.PricePerUnit, date));
                existingByKey[(price.Isin, date)] = toAdd[^1];
            }
        }

        if (toAdd.Count > 0)
            await context.StockPrices.AddRangeAsync(toAdd);

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

    public async Task<StockPrice> Update(string isin, decimal pricePerUnit, Currency currency, DateTime date)
    {
        var stockPrice = await context.StockPrices
            .Include(x => x.StockDetails)
            .FirstOrDefaultAsync(x => x.StockDetails!.Isin == isin && x.Date == date.Date)
            ?? throw new Exception("Update failed");

        var stockDetails = await GetOrCreateStockDetails(isin, currency);

        stockPrice.PricePerUnit = pricePerUnit;
        stockPrice.StockDetails = stockDetails;

        await context.SaveChangesAsync();

        return stockPrice.ToStockPrice();
    }

    private async Task<StockDetails> GetOrCreateStockDetails(string isin, Currency currency)
    {
        var existing = await context.StockDetails.Include(x => x.Currency).FirstOrDefaultAsync(x => x.Isin == isin);
        if (existing is not null)
        {
            if (existing.Currency.Id != currency.Id)
                existing.Currency = currency;
            return existing;
        }

        var created = new StockDetails
        {
            Isin = isin,
            Ticker = string.Empty,
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