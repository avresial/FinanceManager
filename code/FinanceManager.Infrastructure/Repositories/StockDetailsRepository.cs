using FinanceManager.Domain.FinancialAccounts.Stock.Entities;
using FinanceManager.Domain.FinancialAccounts.Stock.Repositories;
using FinanceManager.Domain.Identity.Repositories;
using FinanceManager.Infrastructure.Contexts;
using Microsoft.EntityFrameworkCore;

namespace FinanceManager.Infrastructure.Repositories;

internal class StockDetailsRepository(AppDbContext context) : IStockDetailsRepository
{
    public async Task<StockDetails?> Get(string isin, CancellationToken ct = default)
    {
        return await context.StockDetails
            .Include(x => x.Currency)
            .FirstOrDefaultAsync(x => x.Isin == isin, ct);
    }

    public async Task<StockDetails?> GetByTicker(string ticker, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(ticker)) return null;
        var normalized = ticker.Trim().ToUpperInvariant();
        return await context.StockDetails
            .Include(x => x.Currency)
            .AsNoTracking()
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
        // Normalize ticker for consistency
        var normalized = details.Ticker.Trim().ToUpperInvariant();
        details.Ticker = normalized;

        if (context.Entry(details.Currency).State == EntityState.Detached)
            context.Attach(details.Currency);

        var existing = await context.StockDetails
            .Include(x => x.Currency)
            .FirstOrDefaultAsync(x => x.Isin == details.Isin, ct);

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
            existing.Ticker = normalized;
            if (!string.IsNullOrWhiteSpace(details.AlphaVantageSymbol))
                existing.AlphaVantageSymbol = details.AlphaVantageSymbol;
        }

        await context.SaveChangesAsync(ct);
        return existing ?? details;
    }

    public async Task<bool> Delete(string isin, CancellationToken ct = default)
    {
        var stockDetails = await context.StockDetails.FirstOrDefaultAsync(x => x.Isin == isin, ct);
        if (stockDetails is null) return false;

        var prices = await context.StockPrices
            .Include(x => x.StockDetails)
            .Where(x => x.StockDetails!.Isin == isin)
            .ToListAsync(ct);

        if (prices.Count > 0)
            context.StockPrices.RemoveRange(prices);

        context.StockDetails.Remove(stockDetails);
        await context.SaveChangesAsync(ct);
        return true;
    }
}