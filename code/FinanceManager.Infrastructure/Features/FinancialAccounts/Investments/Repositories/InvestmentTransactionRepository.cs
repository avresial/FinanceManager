using FinanceManager.Domain.FinancialAccounts.Investments.Entities;
using FinanceManager.Domain.FinancialAccounts.Investments.Repositories;
using FinanceManager.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FinanceManager.Infrastructure.Features.FinancialAccounts.Investments.Repositories;

public class InvestmentTransactionRepository(AppDbContext context) : IInvestmentTransactionRepository
{
    public Task<InvestmentTransaction?> Get(long id, CancellationToken cancellationToken = default) =>
        context.InvestmentTransactions
            .Include(x => x.AssetListing)
            .ThenInclude(x => x.Asset)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public async Task<IReadOnlyList<InvestmentTransaction>> GetByAccount(int accountId, CancellationToken cancellationToken = default) =>
        await context.InvestmentTransactions.AsNoTracking()
            .Include(x => x.AssetListing)
            .ThenInclude(x => x.Asset)
            .Where(x => x.AccountId == accountId)
            .OrderBy(x => x.TradeDate).ThenBy(x => x.Id)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<InvestmentTransaction>> GetByAccounts(IReadOnlyCollection<int> accountIds, CancellationToken cancellationToken = default)
    {
        if (accountIds.Count == 0) return [];

        return await context.InvestmentTransactions.AsNoTracking()
            .Include(x => x.AssetListing)
            .Where(x => accountIds.Contains(x.AccountId))
            .OrderBy(x => x.TradeDate).ThenBy(x => x.Id)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<InvestmentTransaction>> GetMostRecentByAccounts(IReadOnlyCollection<int> accountIds, int count, CancellationToken cancellationToken = default)
    {
        if (accountIds.Count == 0 || count <= 0) return [];

        return await context.InvestmentTransactions.AsNoTracking()
            .Include(x => x.AssetListing)
            .Where(x => accountIds.Contains(x.AccountId))
            .OrderByDescending(x => x.TradeDate).ThenByDescending(x => x.Id)
            .Take(count)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<InvestmentTransaction>> GetByUser(long userId, DateOnly startDate, DateOnly endDate, CancellationToken cancellationToken = default) =>
        await context.InvestmentTransactions.AsNoTracking()
            .Include(x => x.AssetListing)
            .Where(x => x.UserId == userId && x.TradeDate >= startDate && x.TradeDate <= endDate)
            .OrderBy(x => x.TradeDate).ThenBy(x => x.Id)
            .ToListAsync(cancellationToken);

    public async Task<InvestmentTransaction> Add(InvestmentTransaction transaction, CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        if (transaction.CreatedAt == default) transaction.CreatedAt = now;
        transaction.UpdatedAt = now;

        context.InvestmentTransactions.Add(transaction);
        await context.SaveChangesAsync(cancellationToken);
        return transaction;
    }

    public async Task<bool> Update(InvestmentTransaction transaction, CancellationToken cancellationToken = default)
    {
        var existing = await context.InvestmentTransactions.FirstOrDefaultAsync(x => x.Id == transaction.Id, cancellationToken);
        if (existing is null) return false;

        existing.AssetListingId = transaction.AssetListingId;
        existing.Type = transaction.Type;
        existing.Quantity = transaction.Quantity;
        existing.UnitPrice = transaction.UnitPrice;
        existing.Currency = transaction.Currency;
        existing.TradeDate = transaction.TradeDate;
        existing.Fee = transaction.Fee;
        existing.Notes = transaction.Notes;
        existing.UpdatedAt = DateTimeOffset.UtcNow;
        await context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> Delete(long id, CancellationToken cancellationToken = default)
    {
        var entity = await context.InvestmentTransactions.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (entity is null) return false;

        context.InvestmentTransactions.Remove(entity);
        await context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<IReadOnlyDictionary<long, decimal>> GetHoldingsAsOf(IReadOnlyCollection<int> accountIds, DateOnly asOf, CancellationToken cancellationToken = default)
    {
        if (accountIds.Count == 0) return new Dictionary<long, decimal>();

        var rows = await context.InvestmentTransactions.AsNoTracking()
            .Where(x => accountIds.Contains(x.AccountId) && x.TradeDate <= asOf)
            .GroupBy(x => x.AssetListingId)
            .Select(g => new
            {
                ListingId = g.Key,
                Holding = g.Sum(t => t.Type == InvestmentTransactionType.Sell ? -t.Quantity : t.Quantity)
            })
            .ToListAsync(cancellationToken);

        return rows.ToDictionary(x => x.ListingId, x => x.Holding);
    }

    public async Task<IReadOnlyDictionary<int, IReadOnlyDictionary<long, decimal>>> GetHoldingsByAccountAsOf(
        IReadOnlyCollection<int> accountIds,
        DateOnly asOf,
        CancellationToken cancellationToken = default)
    {
        if (accountIds.Count == 0) return new Dictionary<int, IReadOnlyDictionary<long, decimal>>();

        var rows = await context.InvestmentTransactions.AsNoTracking()
            .Where(x => accountIds.Contains(x.AccountId) && x.TradeDate <= asOf)
            .GroupBy(x => new { x.AccountId, x.AssetListingId })
            .Select(g => new
            {
                g.Key.AccountId,
                g.Key.AssetListingId,
                Holding = g.Sum(t => t.Type == InvestmentTransactionType.Sell ? -t.Quantity : t.Quantity)
            })
            .ToListAsync(cancellationToken);

        return rows
            .GroupBy(x => x.AccountId)
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlyDictionary<long, decimal>)g.ToDictionary(x => x.AssetListingId, x => x.Holding));
    }

    public async Task<IInvestmentTransactionRepository.AccountValuationInputs> GetValuationInputs(
        IReadOnlyCollection<int> accountIds,
        DateOnly startDate,
        DateOnly endDate,
        CancellationToken cancellationToken = default)
    {
        if (accountIds.Count == 0 || startDate > endDate)
            return new IInvestmentTransactionRepository.AccountValuationInputs([], []);

        var openingRows = await context.InvestmentTransactions.AsNoTracking()
            .Where(x => accountIds.Contains(x.AccountId) && x.TradeDate < startDate)
            .GroupBy(x => new { x.AccountId, x.AssetListingId })
            .Select(g => new
            {
                g.Key.AccountId,
                g.Key.AssetListingId,
                Quantity = g.Sum(t => t.Type == InvestmentTransactionType.Sell ? -t.Quantity : t.Quantity)
            })
            .Where(x => x.Quantity != 0m)
            .ToListAsync(cancellationToken);

        var tradeRows = await context.InvestmentTransactions.AsNoTracking()
            .Where(x => accountIds.Contains(x.AccountId) && x.TradeDate >= startDate && x.TradeDate <= endDate)
            .GroupBy(x => new { x.AccountId, x.AssetListingId, x.TradeDate })
            .Select(g => new
            {
                g.Key.AccountId,
                g.Key.AssetListingId,
                g.Key.TradeDate,
                SignedQuantity = g.Sum(t => t.Type == InvestmentTransactionType.Sell ? -t.Quantity : t.Quantity)
            })
            .Where(x => x.SignedQuantity != 0m)
            .ToListAsync(cancellationToken);

        var openingPositions = openingRows
            .Select(x => new IInvestmentTransactionRepository.ValuationOpeningPosition(x.AccountId, x.AssetListingId, x.Quantity))
            .ToList();

        var trades = tradeRows
            .Select(x => new IInvestmentTransactionRepository.ValuationTradeInput(x.AccountId, x.AssetListingId, x.TradeDate, x.SignedQuantity))
            .ToList();

        return new IInvestmentTransactionRepository.AccountValuationInputs(openingPositions, trades);
    }

    public async Task<IReadOnlyList<IInvestmentTransactionRepository.CapitalFlowInput>> GetCapitalFlowInputs(
        IReadOnlyCollection<int> accountIds,
        DateOnly toDate,
        CancellationToken cancellationToken = default)
    {
        if (accountIds.Count == 0) return [];

        return await context.InvestmentTransactions.AsNoTracking()
            .Where(x => accountIds.Contains(x.AccountId) && x.TradeDate <= toDate)
            .OrderBy(x => x.TradeDate).ThenBy(x => x.Id)
            .Select(x => new IInvestmentTransactionRepository.CapitalFlowInput(
                x.AccountId,
                x.TradeDate,
                x.Type,
                x.Quantity,
                x.UnitPrice,
                x.Fee,
                x.Currency,
                (decimal?)x.AssetListing.PriceMultiplier))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<long>> GetDistinctAssetListingIds(CancellationToken cancellationToken = default) =>
        await context.InvestmentTransactions.AsNoTracking()
            .Select(x => x.AssetListingId)
            .Distinct()
            .OrderBy(x => x)
            .ToListAsync(cancellationToken);
}