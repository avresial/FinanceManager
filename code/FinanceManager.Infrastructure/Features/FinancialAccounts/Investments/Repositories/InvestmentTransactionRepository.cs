using FinanceManager.Domain.FinancialAccounts.Investments.Entities;
using FinanceManager.Domain.FinancialAccounts.Investments.Repositories;
using FinanceManager.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FinanceManager.Infrastructure.Features.FinancialAccounts.Investments.Repositories;

public class InvestmentTransactionRepository(AppDbContext context) : IInvestmentTransactionRepository
{
    private async Task<List<FinanceManager.Domain.FinancialAccounts.Shared.Entities.FinancialLabel>> ResolveLabels(
        ICollection<FinanceManager.Domain.FinancialAccounts.Shared.Entities.FinancialLabel> labels, CancellationToken cancellationToken)
    {
        if (labels.Count == 0) return [];

        var ids = labels.Select(x => x.Id).Distinct().ToArray();
        return await context.FinancialLabels.Where(x => ids.Contains(x.Id)).ToListAsync(cancellationToken);
    }

    public Task<InvestmentTransaction?> Get(long id, CancellationToken cancellationToken = default) =>
        context.InvestmentTransactions
            .AsSplitQuery().Include(x => x.Labels)
            .Include(x => x.AssetListing)
            .ThenInclude(x => x.Asset)
            .Include(x => x.AssetListing)
            .ThenInclude(x => x.MarketDataSymbols)
            .FirstOrDefaultAsync(x => x.EntryId == id, cancellationToken);

    public async Task<IReadOnlyList<InvestmentTransaction>> GetByAccount(int accountId, CancellationToken cancellationToken = default) =>
        await context.InvestmentTransactions.AsNoTracking()
            .AsSplitQuery().Include(x => x.Labels)
            .Include(x => x.AssetListing)
            .ThenInclude(x => x.Asset)
            .Include(x => x.AssetListing)
            .ThenInclude(x => x.MarketDataSymbols)
            .Where(x => x.AccountId == accountId)
            .OrderBy(x => x.PostingDate).ThenBy(x => x.EntryId)
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
            .Include(x => x.AssetListing)
            .ThenInclude(x => x.MarketDataSymbols)
            .Where(x => x.AccountId == accountId && transactionIds.Contains(x.EntryId))
            .OrderBy(x => x.PostingDate).ThenBy(x => x.EntryId)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<InvestmentTransaction>> GetMostRecentByAccounts(IReadOnlyCollection<int> accountIds, int count, CancellationToken cancellationToken = default)
    {
        if (accountIds.Count == 0 || count <= 0) return [];

        return await context.InvestmentTransactions.AsNoTracking()
            .Include(x => x.AssetListing)
            .Where(x => accountIds.Contains(x.AccountId))
            .OrderByDescending(x => x.PostingDate).ThenByDescending(x => x.EntryId)
            .Take(count)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<InvestmentTransaction>> GetByUser(long userId, DateOnly startDate, DateOnly endDate, CancellationToken cancellationToken = default) =>
        await context.InvestmentTransactions.AsNoTracking()
            .Include(x => x.AssetListing)
            .Where(x => x.UserId == userId && x.PostingDate >= startDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc) && x.PostingDate <= endDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc))
            .OrderBy(x => x.PostingDate).ThenBy(x => x.EntryId)
            .ToListAsync(cancellationToken);

    public async Task<InvestmentTransaction> Add(InvestmentTransaction transaction, CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        if (transaction.CreatedAt == default) transaction.CreatedAt = now;
        transaction.UpdatedAt = now;

        transaction.Labels = await ResolveLabels(transaction.Labels, cancellationToken);
        await InTransaction(async () =>
        {
            context.InvestmentTransactions.Add(transaction);
            await context.SaveChangesAsync(cancellationToken);
            await RecalculateValues([(transaction.AccountId, transaction.AssetListingId)], cancellationToken);
        }, cancellationToken);
        return transaction;
    }

    public async Task<bool> Update(InvestmentTransaction transaction, CancellationToken cancellationToken = default)
    {
        var existing = await context.InvestmentTransactions.FirstOrDefaultAsync(x => x.EntryId == transaction.EntryId, cancellationToken);
        if (existing is null) return false;

        // A changed listing moves the row between two running-sum partitions, so both must be recalculated.
        var previousPartition = (existing.AccountId, existing.AssetListingId);
        await context.Entry(existing).Collection(x => x.Labels).LoadAsync(cancellationToken);
        existing.Labels = await ResolveLabels(transaction.Labels, cancellationToken);
        existing.AssetListingId = transaction.AssetListingId;
        existing.Type = transaction.Type;
        existing.Quantity = transaction.Quantity;
        existing.UnitPrice = transaction.UnitPrice;
        existing.Currency = transaction.Currency;
        existing.TradeDate = transaction.TradeDate;
        existing.Fee = transaction.Fee;
        existing.Description = transaction.Description;
        existing.UpdatedAt = DateTimeOffset.UtcNow;
        await InTransaction(async () =>
        {
            await context.SaveChangesAsync(cancellationToken);
            await RecalculateValues([previousPartition, (existing.AccountId, existing.AssetListingId)], cancellationToken);
        }, cancellationToken);
        return true;
    }

    public async Task<bool> Delete(long id, CancellationToken cancellationToken = default)
    {
        var entity = await context.InvestmentTransactions.FirstOrDefaultAsync(x => x.EntryId == id, cancellationToken);
        if (entity is null) return false;

        await InTransaction(async () =>
        {
            context.InvestmentTransactions.Remove(entity);
            await context.SaveChangesAsync(cancellationToken);
            await RecalculateValues([(entity.AccountId, entity.AssetListingId)], cancellationToken);
        }, cancellationToken);
        return true;
    }

    private async Task RecalculateValues(IEnumerable<(int AccountId, long AssetListingId)> partitions, CancellationToken cancellationToken)
    {
        if (await new InvestmentTransactionValueCalculator(context).Recalculate(partitions, cancellationToken) > 0)
            await context.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Runs the trade change and the running-value recalculation in one database transaction, so a failure in
    /// either leaves both untouched. Providers without transactions (InMemory) just run the work.
    /// </summary>
    private async Task InTransaction(Func<Task> work, CancellationToken cancellationToken)
    {
        if (!context.Database.IsRelational() || context.Database.CurrentTransaction is not null)
        {
            await work();
            return;
        }

        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        await work();
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<IReadOnlyDictionary<long, decimal>> GetHoldingsAsOf(IReadOnlyCollection<int> accountIds, DateOnly asOf, CancellationToken cancellationToken = default)
    {
        if (accountIds.Count == 0) return new Dictionary<long, decimal>();

        var rows = await context.InvestmentTransactions.AsNoTracking()
            .Where(x => accountIds.Contains(x.AccountId) && x.PostingDate <= asOf.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc))
            .GroupBy(x => x.AssetListingId)
            .Select(g => new
            {
                ListingId = g.Key,
                Holding = g.Sum(t => t.Type == InvestmentTransactionType.Buy
                    ? t.Quantity
                    : t.Type == InvestmentTransactionType.Sell ? -t.Quantity : 0m)
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
            .Where(x => accountIds.Contains(x.AccountId) && x.PostingDate <= asOf.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc))
            .GroupBy(x => new { x.AccountId, x.AssetListingId })
            .Select(g => new
            {
                g.Key.AccountId,
                g.Key.AssetListingId,
                Holding = g.Sum(t => t.Type == InvestmentTransactionType.Buy
                    ? t.Quantity
                    : t.Type == InvestmentTransactionType.Sell ? -t.Quantity : 0m)
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
            .Where(x => accountIds.Contains(x.AccountId) && x.PostingDate < startDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc))
            .GroupBy(x => new { x.AccountId, x.AssetListingId })
            .Select(g => new
            {
                g.Key.AccountId,
                g.Key.AssetListingId,
                Quantity = g.Sum(t => t.Type == InvestmentTransactionType.Buy
                    ? t.Quantity
                    : t.Type == InvestmentTransactionType.Sell ? -t.Quantity : 0m)
            })
            .Where(x => x.Quantity != 0m)
            .ToListAsync(cancellationToken);

        var tradeRows = await context.InvestmentTransactions.AsNoTracking()
            .Where(x => accountIds.Contains(x.AccountId) && x.PostingDate >= startDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc) && x.PostingDate <= endDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc))
            .GroupBy(x => new { x.AccountId, x.AssetListingId, x.PostingDate })
            .Select(g => new
            {
                g.Key.AccountId,
                g.Key.AssetListingId,
                g.Key.PostingDate,
                SignedQuantity = g.Sum(t => t.Type == InvestmentTransactionType.Buy
                    ? t.Quantity
                    : t.Type == InvestmentTransactionType.Sell ? -t.Quantity : 0m)
            })
            .Where(x => x.SignedQuantity != 0m)
            .ToListAsync(cancellationToken);

        var openingPositions = openingRows
            .Select(x => new IInvestmentTransactionRepository.ValuationOpeningPosition(x.AccountId, x.AssetListingId, x.Quantity))
            .ToList();

        var trades = tradeRows
            .Select(x => new IInvestmentTransactionRepository.ValuationTradeInput(x.AccountId, x.AssetListingId, DateOnly.FromDateTime(x.PostingDate), x.SignedQuantity))
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
            .Where(x => accountIds.Contains(x.AccountId) && x.PostingDate <= toDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc))
            .OrderBy(x => x.PostingDate).ThenBy(x => x.EntryId)
            .Select(x => new IInvestmentTransactionRepository.CapitalFlowInput(
                x.EntryId,
                x.AccountId,
                x.AssetListingId,
                DateOnly.FromDateTime(x.PostingDate),
                x.Type,
                x.Quantity,
                x.UnitPrice,
                x.Fee,
                x.Currency,
                (decimal?)x.AssetListing.PriceMultiplier))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<InvestmentTransaction>> GetLatestByListingAsOf(
        int accountId,
        DateOnly asOf,
        CancellationToken cancellationToken = default)
    {
        if (accountId <= 0) return [];

        var latestIds = context.InvestmentTransactions.AsNoTracking()
            .Where(x => x.AccountId == accountId && x.PostingDate <= asOf.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc))
            .GroupBy(x => x.AssetListingId)
            .Select(group => group
                .OrderByDescending(x => x.PostingDate)
                .ThenByDescending(x => x.EntryId)
                .Select(x => x.EntryId)
                .First());

        return await context.InvestmentTransactions.AsNoTracking()
            .AsSplitQuery().Include(x => x.Labels)
            .Include(x => x.AssetListing)
            .ThenInclude(x => x.Asset)
            .Include(x => x.AssetListing)
            .ThenInclude(x => x.MarketDataSymbols)
            .Where(x => latestIds.Contains(x.EntryId))
            .OrderBy(x => x.AssetListingId)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<long>> GetDistinctAssetListingIds(CancellationToken cancellationToken = default) =>
        await context.InvestmentTransactions.AsNoTracking()
            .Select(x => x.AssetListingId)
            .Distinct()
            .OrderBy(x => x)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<InvestmentTransaction>> GetHistoryPage(
        IReadOnlyCollection<int> accountIds,
        DateOnly startDate,
        DateOnly endDate,
        int take,
        DateOnly? cursorTradeDate = null,
        long? cursorId = null,
        CancellationToken cancellationToken = default,
        string? search = null,
        IReadOnlyCollection<int>? searchAccountIds = null)
    {
        if (cursorTradeDate.HasValue != cursorId.HasValue)
            throw new ArgumentException("Both cursor trade date and transaction id must be supplied together.");
        if (startDate > endDate)
            throw new ArgumentException("Start date must be on or before end date.", nameof(startDate));
        if (accountIds.Count == 0 || take <= 0) return [];

        var query = context.InvestmentTransactions.AsNoTracking()
            .Where(x => accountIds.Contains(x.AccountId) && x.PostingDate >= startDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc) && x.PostingDate <= endDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc));

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToUpperInvariant();
            var matchedAccounts = searchAccountIds ?? [];
            query = query.Where(x => matchedAccounts.Contains(x.AccountId) ||
                x.AssetListing.Ticker.ToUpper().Contains(term) ||
                x.AssetListing.Asset.Name.ToUpper().Contains(term));
        }

        if (cursorTradeDate is DateOnly date && cursorId is long id)
            query = query.Where(x => x.PostingDate < date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc) || (x.PostingDate == date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc) && x.EntryId < id));

        return await query
            .Include(x => x.AssetListing)
            .ThenInclude(x => x.Asset)
            .Include(x => x.AssetListing)
            .ThenInclude(x => x.MarketDataSymbols)
            .OrderByDescending(x => x.PostingDate)
            .ThenByDescending(x => x.EntryId)
            .Take(take)
            .ToListAsync(cancellationToken);
    }

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
            .AsSplitQuery().Include(x => x.Labels)
            .Include(x => x.AssetListing)
            .ThenInclude(x => x.Asset)
            .Include(x => x.AssetListing)
            .ThenInclude(x => x.MarketDataSymbols)
            .Where(x => x.AccountId == accountId);

        if (cursorTradeDate.HasValue && cursorId.HasValue)
        {
            var cd = cursorTradeDate.Value;
            var cid = cursorId.Value;
            query = query.Where(x => x.PostingDate < cd.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc) || (x.PostingDate == cd.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc) && x.EntryId < cid));
        }

        if (startDate.HasValue)
            query = query.Where(x => x.PostingDate >= startDate.Value.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc));

        if (endDate.HasValue)
            query = query.Where(x => x.PostingDate <= endDate.Value.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc));

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
                EF.Functions.Like(x.Description.ToUpper(), likePattern, "\\") ||
                EF.Functions.Like(x.Currency.ToUpper(), likePattern, "\\") ||
                EF.Functions.Like(x.AssetListing.Ticker.ToUpper(), likePattern, "\\") ||
                EF.Functions.Like(x.AssetListing.ExchangeName.ToUpper(), likePattern, "\\") ||
                EF.Functions.Like(x.AssetListing.Asset.Name.ToUpper(), likePattern, "\\") ||
                (x.AssetListing.Asset.Isin != null && EF.Functions.Like(x.AssetListing.Asset.Isin.ToUpper(), likePattern, "\\")));
        }

        var rows = await query
            .OrderByDescending(x => x.PostingDate)
            .ThenByDescending(x => x.EntryId)
            .Take(pageSize + 1)
            .ToListAsync(cancellationToken);

        var hasMore = rows.Count > pageSize;
        var items = hasMore ? rows.Take(pageSize).ToList() : rows;
        return (items, hasMore);
    }
}