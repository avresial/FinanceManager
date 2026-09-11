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

    public async Task<IReadOnlyList<InvestmentTransaction>> GetByAccountAndIds(
        int accountId,
        IReadOnlyCollection<long> transactionIds,
        CancellationToken cancellationToken = default)
    {
        if (accountId <= 0 || transactionIds.Count == 0) return [];

        return await context.InvestmentTransactions.AsNoTracking()
            .Include(x => x.AssetListing)
            .ThenInclude(x => x.Asset)
            .Where(x => x.AccountId == accountId && transactionIds.Contains(x.Id))
            .OrderBy(x => x.TradeDate).ThenBy(x => x.Id)
            .ToListAsync(cancellationToken);
    }

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
            .Select(x => new { x.AssetListingId, x.Type, x.Quantity })
            .ToListAsync(cancellationToken);

        return rows
            .GroupBy(x => x.AssetListingId)
            .ToDictionary(
                g => g.Key,
                g => g.Sum(t => t.Type == InvestmentTransactionType.Sell ? -t.Quantity : t.Quantity));
    }

    public async Task<IReadOnlyList<InvestmentTransaction>> GetLatestByListingAsOf(
        int accountId,
        DateOnly asOf,
        CancellationToken cancellationToken = default)
    {
        if (accountId <= 0) return [];

        var latestIds = context.InvestmentTransactions.AsNoTracking()
            .Where(x => x.AccountId == accountId && x.TradeDate <= asOf)
            .GroupBy(x => x.AssetListingId)
            .Select(group => group
                .OrderByDescending(x => x.TradeDate)
                .ThenByDescending(x => x.Id)
                .Select(x => x.Id)
                .First());

        return await context.InvestmentTransactions.AsNoTracking()
            .Include(x => x.AssetListing)
            .ThenInclude(x => x.Asset)
            .Where(x => latestIds.Contains(x.Id))
            .OrderBy(x => x.AssetListingId)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<long>> GetDistinctAssetListingIds(CancellationToken cancellationToken = default) =>
        await context.InvestmentTransactions.AsNoTracking()
            .Select(x => x.AssetListingId)
            .Distinct()
            .OrderBy(x => x)
            .ToListAsync(cancellationToken);

    public async Task<(IReadOnlyList<InvestmentTransaction> Items, bool HasMore)> GetHistoryPage(
        int accountId,
        int pageSize,
        DateOnly? cursorTradeDate = null,
        long? cursorId = null,
        DateOnly? startDate = null,
        DateOnly? endDate = null,
        InvestmentTransactionType? type = null,
        string? search = null,
        CancellationToken cancellationToken = default)
    {
        if (pageSize <= 0) return ([], false);

        var query = context.InvestmentTransactions.AsNoTracking()
            .Include(x => x.AssetListing)
            .ThenInclude(x => x.Asset)
            .Where(x => x.AccountId == accountId);

        if (cursorTradeDate.HasValue && cursorId.HasValue)
        {
            var cd = cursorTradeDate.Value;
            var cid = cursorId.Value;
            query = query.Where(x => x.TradeDate < cd || (x.TradeDate == cd && x.Id < cid));
        }

        if (startDate.HasValue)
            query = query.Where(x => x.TradeDate >= startDate.Value);

        if (endDate.HasValue)
            query = query.Where(x => x.TradeDate <= endDate.Value);

        if (type.HasValue)
            query = query.Where(x => x.Type == type.Value);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            var escapedQuery = s.ToUpperInvariant()
                .Replace("\\", "\\\\", StringComparison.Ordinal)
                .Replace("%", "\\%", StringComparison.Ordinal)
                .Replace("_", "\\_", StringComparison.Ordinal);
            var likePattern = $"%{escapedQuery}%";
            query = query.Where(x =>
                (x.Notes != null && EF.Functions.Like(x.Notes.ToUpper(), likePattern, "\\")) ||
                EF.Functions.Like(x.Currency.ToUpper(), likePattern, "\\") ||
                EF.Functions.Like(x.AssetListing.Ticker.ToUpper(), likePattern, "\\") ||
                EF.Functions.Like(x.AssetListing.ExchangeName.ToUpper(), likePattern, "\\") ||
                EF.Functions.Like(x.AssetListing.Asset.Name.ToUpper(), likePattern, "\\") ||
                (x.AssetListing.Asset.Isin != null && EF.Functions.Like(x.AssetListing.Asset.Isin.ToUpper(), likePattern, "\\")));
        }

        var rows = await query
            .OrderByDescending(x => x.TradeDate)
            .ThenByDescending(x => x.Id)
            .Take(pageSize + 1)
            .ToListAsync(cancellationToken);

        var hasMore = rows.Count > pageSize;
        var items = hasMore ? rows.Take(pageSize).ToList() : rows;
        return (items, hasMore);
    }
}