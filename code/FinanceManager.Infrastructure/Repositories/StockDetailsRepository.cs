using FinanceManager.Domain.Entities.Stocks;
using FinanceManager.Domain.Repositories;
using FinanceManager.Infrastructure.Contexts;
using Microsoft.EntityFrameworkCore;

namespace FinanceManager.Infrastructure.Repositories;

internal class StockDetailsRepository(AppDbContext context) : IStockDetailsRepository
{
    public async Task<StockDetails?> Get(string ticker, CancellationToken ct = default)
    {
        var normalized = ticker.Trim().ToUpperInvariant();
        return await context.StockDetails
            .Include(x => x.Currency)
            .FirstOrDefaultAsync(x => x.Ticker == normalized, ct);
    }

    public async Task<IReadOnlyList<StockDetails>> GetAll(CancellationToken ct = default)
    {
        return await context.StockDetails
            .Include(x => x.Currency)
            .AsNoTracking()
            .OrderBy(x => x.Ticker)
            .ToListAsync(ct);
    }

    public async Task<StockDetails> Add(StockDetails details, CancellationToken ct = default)
    {
        var normalized = details.Ticker.Trim().ToUpperInvariant();
        details.Ticker = normalized;

        if (context.Entry(details.Currency).State == EntityState.Detached)
            context.Attach(details.Currency);

        var existing = await context.StockDetails
            .Include(x => x.Currency)
            .FirstOrDefaultAsync(x => x.Ticker == normalized, ct);

        if (existing is null)
        {
            context.StockDetails.Add(details);
        }
        else
        {
            existing.Name = details.Name;
            existing.Type = details.Type;
            existing.Region = details.Region;
            existing.Currency = details.Currency;
        }

        await context.SaveChangesAsync(ct);
        return existing ?? details;
    }

    public async Task<bool> Delete(string ticker, CancellationToken ct = default)
    {
        var normalized = ticker.Trim().ToUpperInvariant();
        var stockDetails = await context.StockDetails.FirstOrDefaultAsync(x => x.Ticker == normalized, ct);
        if (stockDetails is null) return false;

        var prices = await context.StockPrices
            .Include(x => x.StockDetails)
            .Where(x => x.StockDetails!.Ticker == normalized)
            .ToListAsync(ct);

        if (prices.Count > 0)
            context.StockPrices.RemoveRange(prices);

        context.StockDetails.Remove(stockDetails);
        await context.SaveChangesAsync(ct);
        return true;
    }
}