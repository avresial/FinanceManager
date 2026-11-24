using FinanceManager.Domain.FinancialAccounts.Investments.Dtos;
using FinanceManager.Domain.FinancialAccounts.Investments.Entities;
using FinanceManager.Domain.FinancialAccounts.Investments.Repositories;
using FinanceManager.Domain.Transactions.Shared.Dtos;
using FinanceManager.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

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

    public async Task<IReadOnlyList<InvestmentTransaction>> GetByUserAndIds(
        long userId,
        IReadOnlyCollection<long> transactionIds,
        CancellationToken cancellationToken = default)
    {
        if (userId == 0 || transactionIds.Count == 0) return [];

        return await context.InvestmentTransactions.AsNoTracking()
            .Where(x => x.UserId == userId && transactionIds.Contains(x.EntryId))
            .OrderBy(x => x.PostingDate).ThenBy(x => x.EntryId)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<InvestmentTransaction>> GetByUser(long userId, DateOnly startDate, DateOnly endDate, CancellationToken cancellationToken = default) =>
        await OwnedTrades(userId, startDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc), endDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc))
            .Include(x => x.AssetListing)
            .OrderBy(x => x.PostingDate).ThenBy(x => x.EntryId)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyDictionary<int, int>> GetCountPerUser(IReadOnlyCollection<int> userIds, CancellationToken cancellationToken = default)
    {
        // Trades carry their owner as a long; ids are ints everywhere else, so the grouped keys narrow back safely.
        var owners = userIds.Where(id => id != 0).Select(id => (long)id).Distinct().ToList();
        if (owners.Count == 0) return new Dictionary<int, int>();

        var rows = await context.InvestmentTransactions.AsNoTracking()
            .Where(x => owners.Contains(x.UserId))
            .GroupBy(x => x.UserId)
            .Select(grouped => new { UserId = grouped.Key, Count = grouped.Count() })
            .ToListAsync(cancellationToken);
        return rows.ToDictionary(x => (int)x.UserId, x => x.Count);
    }

    private IQueryable<InvestmentTransaction> OwnedTrades(long userId, DateTime? start, DateTime? end)
    {
        ArgumentOutOfRangeException.ThrowIfZero(userId);
        var trades = context.InvestmentTransactions.AsNoTracking().Where(x => x.UserId == userId);
        if (start.HasValue) trades = trades.Where(x => x.PostingDate >= start.Value);
        if (end.HasValue) trades = trades.Where(x => x.PostingDate <= end.Value);
        return trades;
    }

    public async Task<IReadOnlyList<InvestmentTransaction>> GetHistoryByUser(long userId, OwnerHistoryQuery query, CancellationToken ct = default)
    {
        if (query.Take <= 0) return [];
        var trades = OwnedTrades(userId, query.Start, query.End);
        if (query.Boundary is OwnerHistoryBoundary cursor)
            trades = trades.Where(x => x.PostingDate < cursor.Date || x.PostingDate == cursor.Date &&
                (cursor.IncludeEqualId ? x.EntryId <= cursor.EntryId : x.EntryId < cursor.EntryId));
        if (query.NormalizedCurrencies() is { Count: > 0 } currencies)
            trades = trades.Where(x => currencies.Contains(x.Currency.ToUpper()));
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim().ToUpperInvariant();
            trades = trades.Where(x => x.AssetListing.Ticker.ToUpper().Contains(term) || x.AssetListing.Asset.Name.ToUpper().Contains(term));
        }
        var page = trades.OrderByDescending(x => x.PostingDate).ThenByDescending(x => x.EntryId).Take(query.Take);
        if (query.IncludeDetails)
            return await page.AsSplitQuery().Include(x => x.Labels).ThenInclude(l => l.Classifications)
                .Include(x => x.AssetListing).ThenInclude(l => l.Asset)
                .Include(x => x.AssetListing).ThenInclude(l => l.MarketDataSymbols).ToListAsync(ct);
        return await page.Select(x => new InvestmentTransaction(x.EntryId)
        {
            PostingDate = x.PostingDate,
            Type = x.Type,
            Quantity = x.Quantity,
            UnitPrice = x.UnitPrice,
            Currency = x.Currency,
            AssetListing = new() { Ticker = x.AssetListing.Ticker }
        }).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<string>> GetCurrenciesByUser(long userId, CancellationToken ct = default) =>
        await OwnedTrades(userId, null, null).Select(x => x.Currency).Distinct().ToListAsync(ct);

    public async Task<InvestmentTransaction> Add(InvestmentTransaction transaction, CancellationToken cancellationToken = default)
    {
        var accountId = transaction.AccountId;
        var now = DateTimeOffset.UtcNow;
        if (transaction.CreatedAt == default) transaction.CreatedAt = now;
        transaction.UpdatedAt = now;

        transaction.Labels = await ResolveLabels(transaction.Labels, cancellationToken);
        await InAccountTransaction(accountId, async () =>
        {
            context.InvestmentTransactions.Add(transaction);
            await context.SaveChangesAsync(cancellationToken);
            await RecalculateValues([(accountId, transaction.AssetListingId)], cancellationToken);
        }, cancellationToken);
        return transaction;
    }

    public async Task<bool> Update(InvestmentTransaction transaction, CancellationToken cancellationToken = default)
    {
        if (await GetAccountId(transaction.EntryId, cancellationToken) is not AccountLink link) return false;
        var accountId = link.AccountId;

        var updated = false;
        await InAccountTransaction(accountId, async () =>
        {
            var existing = await context.InvestmentTransactions.FirstOrDefaultAsync(x => x.EntryId == transaction.EntryId, cancellationToken);
            if (existing is null) return;

            // A changed listing moves the row between two running-sum partitions, so both must be recalculated.
            // Original values are used because a caller may pass the tracked instance with its listing already changed.
            var entry = context.Entry(existing);
            var originalAccountId = entry.Property(x => x.AccountId).OriginalValue;
            var previousPartition = (originalAccountId, entry.Property(x => x.AssetListingId).OriginalValue);
            await entry.Collection(x => x.Labels).LoadAsync(cancellationToken);
            existing.Labels = await ResolveLabels(transaction.Labels, cancellationToken);
            // Owner and account link are never reassigned; an unlinked (owner-wide) trade stays unlinked.
            existing.AccountId = originalAccountId;
            existing.UserId = entry.Property(x => x.UserId).OriginalValue;
            existing.AssetListingId = transaction.AssetListingId;
            existing.Type = transaction.Type;
            existing.Quantity = transaction.Quantity;
            existing.UnitPrice = transaction.UnitPrice;
            existing.Currency = transaction.Currency;
            existing.TradeDate = transaction.TradeDate;
            existing.Fee = transaction.Fee;
            existing.Description = transaction.Description;
            existing.UpdatedAt = DateTimeOffset.UtcNow;
            await context.SaveChangesAsync(cancellationToken);
            await RecalculateValues([previousPartition, (existing.AccountId, existing.AssetListingId)], cancellationToken);
            updated = true;
        }, cancellationToken);
        return updated;
    }

    public async Task<bool> Delete(long id, CancellationToken cancellationToken = default)
    {
        if (await GetAccountId(id, cancellationToken) is not AccountLink link) return false;
        var accountId = link.AccountId;

        var deleted = false;
        await InAccountTransaction(accountId, async () =>
        {
            var entity = await context.InvestmentTransactions.FirstOrDefaultAsync(x => x.EntryId == id, cancellationToken);
            if (entity is null) return;

            context.InvestmentTransactions.Remove(entity);
            await context.SaveChangesAsync(cancellationToken);
            await RecalculateValues([(accountId, entity.AssetListingId)], cancellationToken);
            deleted = true;
        }, cancellationToken);
        return deleted;
    }

    /// <summary>Returns the trade's account link, or <c>null</c> when the trade does not exist. An unlinked (owner-wide) trade returns a link holding <c>null</c>.</summary>
    private Task<AccountLink?> GetAccountId(long entryId, CancellationToken cancellationToken) =>
        context.InvestmentTransactions.AsNoTracking()
            .Where(x => x.EntryId == entryId)
            .Select(x => new AccountLink(x.AccountId))
            .FirstOrDefaultAsync(cancellationToken);

    private sealed record AccountLink(int? AccountId);

    /// <summary>Running units exist only per account; unlinked trades have no partition to recalculate.</summary>
    private async Task RecalculateValues(IEnumerable<(int? AccountId, long AssetListingId)> partitions, CancellationToken cancellationToken)
    {
        var linked = partitions.Where(x => x.AccountId.HasValue).Select(x => (x.AccountId!.Value, x.AssetListingId)).ToList();
        if (linked.Count > 0 && await new InvestmentTransactionValueCalculator(context).Recalculate(linked, cancellationToken) > 0)
            await context.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Runs the trade change and the running-value recalculation in one database transaction, so a failure in
    /// either leaves both untouched. Providers without transactions (InMemory) just run the work.
    /// <para>
    /// A no-op update on the account row comes first and holds that row's lock until commit. Concurrent writers to the
    /// same account therefore run one at a time and see each other's committed trades, on every relational provider.
    /// Without it, two writers could each calculate a running sum that misses the other's uncommitted trade.
    /// </para>
    /// </summary>
    private async Task InAccountTransaction(int? accountId, Func<Task> work, CancellationToken cancellationToken)
    {
        // An unlinked trade has no running-value partition to protect, so its single SaveChanges is already atomic.
        if (accountId is not int linkedAccountId || !context.Database.IsRelational())
        {
            await work();
            return;
        }

        var ownsTransaction = context.Database.CurrentTransaction is null;
        await using var transaction = ownsTransaction ? await context.Database.BeginTransactionAsync(cancellationToken) : null;
        await context.Accounts
            .Where(x => x.AccountId == linkedAccountId)
            .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.Name, x => x.Name), cancellationToken);
        await work();
        if (transaction is not null)
            await transaction.CommitAsync(cancellationToken);
    }

    public async Task<IReadOnlyDictionary<long, decimal>> GetHoldingsAsOf(IReadOnlyCollection<int> accountIds, DateOnly asOf, CancellationToken cancellationToken = default)
    {
        if (accountIds.Count == 0) return new Dictionary<long, decimal>();

        var rows = await context.InvestmentTransactions.AsNoTracking()
            .Where(x => x.AccountId.HasValue && accountIds.Contains(x.AccountId.Value) && x.PostingDate <= asOf.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc))
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

    public async Task<IReadOnlyList<InvestmentHolding>> GetHoldingsByUserAsOf(long userId, DateOnly asOf, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfZero(userId);

        var units = await context.InvestmentTransactions.AsNoTracking()
            .Where(x => x.UserId == userId && x.PostingDate <= asOf.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc))
            .GroupBy(x => x.AssetListingId)
            .Select(g => new
            {
                ListingId = g.Key,
                Units = g.Sum(t => t.Type == InvestmentTransactionType.Buy
                    ? t.Quantity
                    : t.Type == InvestmentTransactionType.Sell ? -t.Quantity : 0m)
            })
            .Where(x => x.Units != 0m)
            .OrderBy(x => x.ListingId)
            .ToListAsync(cancellationToken);
        if (units.Count == 0) return [];

        var ids = units.Select(x => x.ListingId).ToArray();
        var listings = await context.AssetListings.AsNoTracking()
            .Where(x => ids.Contains(x.Id))
            .Select(x => new { x.Id, x.Ticker, x.ExchangeName, x.Asset.Type })
            .ToDictionaryAsync(x => x.Id, cancellationToken);
        return [.. units.Select(x => new InvestmentHolding(x.ListingId, listings[x.ListingId].Ticker, listings[x.ListingId].ExchangeName, listings[x.ListingId].Type, x.Units))];
    }

    public async Task<IInvestmentTransactionRepository.UserValuationInputs> GetValuationInputsByUser(
        long userId,
        DateOnly startDate,
        DateOnly endDate,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfZero(userId);
        if (startDate > endDate) return new IInvestmentTransactionRepository.UserValuationInputs([], []);

        var start = startDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var end = endDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);

        var openingRows = await context.InvestmentTransactions.AsNoTracking()
            .Where(x => x.UserId == userId && x.PostingDate < start)
            .GroupBy(x => x.AssetListingId)
            .Select(g => new
            {
                AssetListingId = g.Key,
                Quantity = g.Sum(t => t.Type == InvestmentTransactionType.Buy
                    ? t.Quantity
                    : t.Type == InvestmentTransactionType.Sell ? -t.Quantity : 0m)
            })
            .Where(x => x.Quantity != 0m)
            .ToListAsync(cancellationToken);

        var tradeRows = await context.InvestmentTransactions.AsNoTracking()
            .Where(x => x.UserId == userId && x.PostingDate >= start && x.PostingDate <= end)
            .GroupBy(x => new { x.AssetListingId, x.PostingDate })
            .Select(g => new
            {
                g.Key.AssetListingId,
                g.Key.PostingDate,
                SignedQuantity = g.Sum(t => t.Type == InvestmentTransactionType.Buy
                    ? t.Quantity
                    : t.Type == InvestmentTransactionType.Sell ? -t.Quantity : 0m)
            })
            .Where(x => x.SignedQuantity != 0m)
            .ToListAsync(cancellationToken);

        return new IInvestmentTransactionRepository.UserValuationInputs(
            [.. openingRows.Select(x => new IInvestmentTransactionRepository.UserValuationOpeningPosition(x.AssetListingId, x.Quantity))],
            [.. tradeRows.Select(x => new IInvestmentTransactionRepository.UserValuationTradeInput(x.AssetListingId, DateOnly.FromDateTime(x.PostingDate), x.SignedQuantity))]);
    }

    public async Task<IReadOnlyList<IInvestmentTransactionRepository.UserCapitalFlowInput>> GetCapitalFlowInputsByUser(
        long userId,
        DateOnly toDate,
        CancellationToken cancellationToken = default)
    {
        if (userId == 0) return [];

        return await context.InvestmentTransactions.AsNoTracking()
            .Where(x => x.UserId == userId && x.PostingDate <= toDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc))
            .OrderBy(x => x.PostingDate).ThenBy(x => x.EntryId)
            .Select(x => new IInvestmentTransactionRepository.UserCapitalFlowInput(
                x.EntryId,
                x.AssetListingId,
                DateOnly.FromDateTime(x.PostingDate),
                x.Type,
                x.Quantity,
                x.UnitPrice,
                x.Fee,
                x.Currency))
            .ToListAsync(cancellationToken);
    }

    private IQueryable<InvestmentTransaction> LatestOwnerTrades(long userId, DateOnly asOf, IReadOnlyCollection<long> listingIds)
    {
        var trades = context.InvestmentTransactions.AsNoTracking();
        var end = asOf.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var ids = trades.Where(t => t.UserId == userId && t.PostingDate <= end && listingIds.Contains(t.AssetListingId))
            .GroupBy(t => t.AssetListingId)
            .Select(g => g.OrderByDescending(t => t.PostingDate).ThenByDescending(t => t.EntryId).Select(t => t.EntryId).First());
        return trades.Where(t => ids.Contains(t.EntryId));
    }

    public async Task<IReadOnlyList<IInvestmentTransactionRepository.ListingTradePrice>> GetLatestPricesByUserAsOf(
        long userId, DateOnly asOf, IReadOnlyCollection<long> listingIds, CancellationToken ct)
    {
        ArgumentOutOfRangeException.ThrowIfZero(userId);
        if (listingIds.Count == 0) return [];

        return await LatestOwnerTrades(userId, asOf, listingIds)
            .Select(t => new IInvestmentTransactionRepository.ListingTradePrice(t.AssetListingId, t.Currency, t.UnitPrice))
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<InvestmentTransaction>> GetLatestUnpricedTradesByUserAsOf(
        long userId, DateOnly asOf, IReadOnlyCollection<long> listingIds, CancellationToken ct)
    {
        ArgumentOutOfRangeException.ThrowIfZero(userId);
        if (listingIds.Count == 0) return [];

        // Recovery saves the trade through Update, which also copies its labels.
        return await LatestOwnerTrades(userId, asOf, listingIds).Where(t => t.UnitPrice <= 0)
            .Include(t => t.Labels).ToListAsync(ct);
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

    public Task<(IReadOnlyList<InvestmentTransaction> Items, bool HasMore)> GetHistoryPage(
        int accountId,
        int pageSize,
        DateOnly? cursorTradeDate = null,
        long? cursorId = null,
        DateOnly? startDate = null,
        DateOnly? endDate = null,
        InvestmentTransactionType? type = null,
        string? search = null,
        CancellationToken cancellationToken = default) =>
        ReadHistoryPage(x => x.AccountId == accountId, pageSize, cursorTradeDate, cursorId, startDate, endDate, null, type, search, cancellationToken);

    public Task<(IReadOnlyList<InvestmentTransaction> Items, bool HasMore)> GetHistoryPageByUser(
        long userId,
        int pageSize,
        DateOnly? cursorTradeDate = null,
        long? cursorId = null,
        DateOnly? startDate = null,
        DateOnly? endDate = null,
        long? assetListingId = null,
        InvestmentTransactionType? type = null,
        string? search = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfZero(userId);
        return ReadHistoryPage(x => x.UserId == userId, pageSize, cursorTradeDate, cursorId, startDate, endDate, assetListingId, type, search, cancellationToken);
    }

    private async Task<(IReadOnlyList<InvestmentTransaction> Items, bool HasMore)> ReadHistoryPage(
        Expression<Func<InvestmentTransaction, bool>> scope,
        int pageSize,
        DateOnly? cursorTradeDate,
        long? cursorId,
        DateOnly? startDate,
        DateOnly? endDate,
        long? assetListingId,
        InvestmentTransactionType? type,
        string? search,
        CancellationToken cancellationToken)
    {
        if (pageSize <= 0) return ([], false);

        var query = context.InvestmentTransactions.AsNoTracking()
            .AsSplitQuery().Include(x => x.Labels)
            .Include(x => x.AssetListing)
            .ThenInclude(x => x.Asset)
            .Include(x => x.AssetListing)
            .ThenInclude(x => x.MarketDataSymbols)
            .Where(scope);

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

        if (assetListingId.HasValue)
            query = query.Where(x => x.AssetListingId == assetListingId.Value);

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