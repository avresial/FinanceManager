using FinanceManager.Domain.Dashboard.Dtos;
using FinanceManager.Domain.Dashboard.Services;
using FinanceManager.Domain.FinancialAccounts.Bond.Entities;
using FinanceManager.Domain.FinancialAccounts.Bond.Repositories;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.FinancialAccounts.Currencies.Repositories;
using FinanceManager.Domain.FinancialAccounts.Investments.Entities;
using FinanceManager.Domain.FinancialAccounts.Investments.Repositories;
using FinanceManager.Domain.FinancialAccounts.Shared.Entities;
using FinanceManager.Domain.FinancialAccounts.Shared.Repositories;
using FinanceManager.Domain.FinancialAccounts.Shared.ValueObjects;

namespace FinanceManager.Application.Dashboard;

/// <summary>
/// Composes the dashboard transaction log by pulling the newest entries from every
/// account of every type (currency, bond, investment) and interleaving them into a
/// single newest-first list. The underlying repositories share a scoped EF Core
/// DbContext, which allows only one active query at a time — so each account stream
/// is fully buffered before the bounded entry queries run, and everything is awaited
/// sequentially.
/// </summary>
public class TransactionLogService(
    ICurrencyAccountRepository<CurrencyAccount> currencyAccountRepository,
    IAccountRepository<BondAccount> bondAccountRepository,
    IAccountRepository<InvestmentAccount> investmentAccountRepository,
    IAccountEntryRepository<CurrencyAccountEntry> currencyEntryRepository,
    IAccountEntryRepository<BondAccountEntry> bondEntryRepository,
    IInvestmentTransactionRepository investmentTransactionRepository,
    IBondDetailsRepository bondDetailsRepository) : ITransactionLogService
{
    private readonly Dictionary<int, string?> _bondNamesCache = [];

    public async Task<List<TransactionLogEntryDto>> GetLastTransactions(int userId, int count, CancellationToken cancellationToken = default)
    {
        if (count <= 0) return [];

        List<TransactionLogEntryDto> result = [];

        var currencyAccounts = await BufferAccounts(currencyAccountRepository.GetAvailableAccounts(userId), cancellationToken);
        if (currencyAccounts.Count != 0)
        {
            var currencyAccountNames = currencyAccounts.ToDictionary(a => a.AccountId, a => a.AccountName);
            var entries = await currencyEntryRepository.GetMostRecentByAccounts(
                [.. currencyAccountNames.Keys], count, cancellationToken);

            result.AddRange(entries.Select(e => new TransactionLogEntryDto(
                e.AccountId,
                currencyAccountNames[e.AccountId],
                AccountType.Currency,
                e.EntryId,
                e.PostingDate,
                e.ValueChange,
                GetCurrencyDescription(e))));
        }

        var bondAccounts = await BufferAccounts(bondAccountRepository.GetAvailableAccounts(userId), cancellationToken);
        if (bondAccounts.Count != 0)
        {
            var bondAccountNames = bondAccounts.ToDictionary(a => a.AccountId, a => a.AccountName);
            var entries = await bondEntryRepository.GetMostRecentByAccounts(
                [.. bondAccountNames.Keys], count, cancellationToken);
            var bondNames = await GetBondNames(entries, cancellationToken);

            result.AddRange(entries.Select(e => new TransactionLogEntryDto(
                e.AccountId,
                bondAccountNames[e.AccountId],
                AccountType.Bond,
                e.EntryId,
                e.PostingDate,
                e.ValueChange,
                bondNames.TryGetValue(e.BondDetailsId, out var name) && name is not null ? name : "Bond")));
        }

        Dictionary<int, string> investmentAccounts = [];
        await foreach (var account in investmentAccountRepository.GetAvailableAccounts(userId).WithCancellation(cancellationToken))
            investmentAccounts[account.AccountId] = account.AccountName;

        if (investmentAccounts.Count != 0)
        {
            var transactions = await investmentTransactionRepository.GetMostRecentByAccounts([.. investmentAccounts.Keys], count, cancellationToken);
            result.AddRange(transactions
                .Select(t => new TransactionLogEntryDto(
                    t.AccountId,
                    investmentAccounts[t.AccountId],
                    AccountType.Stock,
                    t.Id,
                    t.TradeDate.ToDateTime(TimeOnly.MinValue),
                    t.SignedQuantity * t.UnitPrice,
                    GetInvestmentDescription(t))));
        }

        return [.. result
            .OrderByDescending(e => e.Date)
            .ThenByDescending(e => e.EntryId)
            .Take(count)];
    }

    public async Task<TransactionLogPage> GetPage(int userId, TransactionLogQuery query, CancellationToken cancellationToken = default)
    {
        // Repositories persist posting dates in UTC; date-only trades share UTC midnight.
        query = query with
        {
            Start = AsUtc(query.Start),
            End = AsUtc(query.End),
            Search = string.IsNullOrWhiteSpace(query.Search) ? null : query.Search.Trim(),
            Cursor = query.Cursor is TransactionLogCursor original ? original with { Date = AsUtc(original.Date) } : null
        };
        ArgumentOutOfRangeException.ThrowIfLessThan(query.PageSize, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(query.PageSize, 100);
        if (query.Start > query.End)
            throw new ArgumentException("Start must be on or before end.", nameof(query));
        if (query.Cursor is TransactionLogCursor invalidCursor &&
            (!Enum.IsDefined(invalidCursor.AccountType) || invalidCursor.EntryId <= 0))
            throw new ArgumentException("Cursor must contain a supported account type and positive entry ID.", nameof(query));

        var end = query.Cursor is TransactionLogCursor cursor && cursor.Date < query.End ? cursor.Date : query.End;
        if (end < query.Start)
            return new TransactionLogPage([], null);

        var take = query.PageSize + 1;
        List<TransactionLogEntryDto> result = [];
        var currencyAccounts = IncludesStream(query, AccountType.Currency)
            ? FilterAccounts(await BufferAccounts(currencyAccountRepository.GetAvailableAccounts(userId), cancellationToken), query)
            : [];
        if (currencyAccounts.Count != 0)
        {
            var names = currencyAccounts.ToDictionary(a => a.AccountId, a => a.AccountName);
            var boundary = GetPageBoundary(query.Cursor, AccountType.Currency, int.MaxValue);
            var entries = await currencyEntryRepository.GetHistoryPage(
                [.. names.Keys], query.Start, end, take, boundary.Date, (int?)boundary.Id, cancellationToken, query.Search, SearchAccountIds(names, query.Search));
            result.AddRange(entries.Select(e => new TransactionLogEntryDto(
                e.AccountId, names[e.AccountId], AccountType.Currency, e.EntryId, e.PostingDate,
                e.ValueChange, GetCurrencyDescription(e))));
        }

        var bondAccounts = IncludesStream(query, AccountType.Bond)
            ? FilterAccounts(await BufferAccounts(bondAccountRepository.GetAvailableAccounts(userId), cancellationToken), query)
            : [];
        if (bondAccounts.Count != 0)
        {
            var names = bondAccounts.ToDictionary(a => a.AccountId, a => a.AccountName);
            var boundary = GetPageBoundary(query.Cursor, AccountType.Bond, int.MaxValue);
            var entries = await bondEntryRepository.GetHistoryPage(
                [.. names.Keys], query.Start, end, take, boundary.Date, (int?)boundary.Id, cancellationToken, query.Search, SearchAccountIds(names, query.Search));
            var bondNames = await GetBondNames(entries, cancellationToken);
            result.AddRange(entries.Select(e => new TransactionLogEntryDto(
                e.AccountId, names[e.AccountId], AccountType.Bond, e.EntryId, e.PostingDate,
                e.ValueChange, bondNames.TryGetValue(e.BondDetailsId, out var name) && name is not null ? name : "Bond")));
        }

        // Investment dates represent midnight. A non-midnight start excludes that day's trades.
        var startDate = DateOnly.FromDateTime(query.Start);
        var endDate = DateOnly.FromDateTime(end);
        if (query.Start.TimeOfDay != TimeSpan.Zero)
        {
            if (startDate == DateOnly.MaxValue)
                return CreatePage(result, query.PageSize);
            startDate = startDate.AddDays(1);
        }
        if (startDate <= endDate && IncludesStream(query, AccountType.Stock))
        {
            var accounts = FilterAccounts(await BufferAccounts(investmentAccountRepository.GetAvailableAccounts(userId), cancellationToken), query);
            if (accounts.Count != 0)
            {
                var names = accounts.ToDictionary(a => a.AccountId, a => a.AccountName);
                // An intraday cursor is later than every trade on that date, regardless of ID.
                var boundary = query.Cursor is TransactionLogCursor stockCursor && stockCursor.Date.TimeOfDay == TimeSpan.Zero
                    ? GetPageBoundary(stockCursor, AccountType.Stock, long.MaxValue)
                    : default;
                var transactions = await investmentTransactionRepository.GetHistoryPage(
                    [.. names.Keys], startDate, endDate, take,
                    boundary.Date is DateTime date ? DateOnly.FromDateTime(date) : null, boundary.Id, cancellationToken, query.Search, SearchAccountIds(names, query.Search));
                result.AddRange(transactions.Select(t => new TransactionLogEntryDto(
                    t.AccountId, names[t.AccountId], AccountType.Stock, t.Id, t.TradeDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
                    t.SignedQuantity * t.UnitPrice, GetInvestmentDescription(t))));
            }
        }

        return CreatePage(result, query.PageSize);
    }

    private static IReadOnlyCollection<int> SearchAccountIds(Dictionary<int, string> accounts, string? search) =>
        search is null ? [] : [.. accounts.Where(a => a.Value.Contains(search, StringComparison.OrdinalIgnoreCase)).Select(a => a.Key)];

    private static bool IncludesStream(TransactionLogQuery query, AccountType type) =>
        query.AccountTypes is not { Count: > 0 } types || types.Contains(type);

    // Intersecting with the user's own accounts (rather than querying the requested IDs directly)
    // is what keeps foreign account IDs from ever reaching an entry query.
    private static List<AvailableAccount> FilterAccounts(List<AvailableAccount> accounts, TransactionLogQuery query) =>
        query.AccountIds is { Count: > 0 } ids ? [.. accounts.Where(a => ids.Contains(a.AccountId))] : accounts;

    private static DateTime AsUtc(DateTime date) => date.Kind == DateTimeKind.Unspecified
        ? DateTime.SpecifyKind(date, DateTimeKind.Utc)
        : date.ToUniversalTime();

    private static (DateTime? Date, long? Id) GetPageBoundary(TransactionLogCursor? cursor, AccountType type, long maximumId)
    {
        if (cursor is null)
            return default;

        // Later streams retain the equal date/ID row. Translate that inclusive ID to the
        // repositories' exclusive boundary; IDs beyond a table's range need only the capped end date.
        var includeEqualId = AccountTypeOrder(type) > AccountTypeOrder(cursor.AccountType);
        if (cursor.EntryId > maximumId || (includeEqualId && cursor.EntryId == maximumId))
            return default;
        return (cursor.Date, cursor.EntryId + (includeEqualId ? 1 : 0));
    }

    private static int AccountTypeOrder(AccountType type) => type switch
    {
        AccountType.Currency => 0,
        AccountType.Bond => 1,
        AccountType.Stock => 2,
        _ => throw new ArgumentOutOfRangeException(nameof(type))
    };

    private static TransactionLogPage CreatePage(List<TransactionLogEntryDto> entries, int pageSize)
    {
        var ordered = entries.OrderByDescending(e => e.Date).ThenByDescending(e => e.EntryId)
            .ThenBy(e => AccountTypeOrder(e.AccountType)).Take(pageSize + 1).ToList();
        TransactionLogCursor? next = ordered.Count > pageSize
            ? new(ordered[pageSize - 1].Date, ordered[pageSize - 1].AccountType, ordered[pageSize - 1].EntryId)
            : null;
        return new TransactionLogPage([.. ordered.Take(pageSize)], next);
    }

    // The account query must finish streaming before any entry query starts: relational
    // providers reject a second command while the previous reader is still open.
    private static async Task<List<AvailableAccount>> BufferAccounts(IAsyncEnumerable<AvailableAccount> accounts, CancellationToken cancellationToken)
    {
        List<AvailableAccount> result = [];
        await foreach (var account in accounts.WithCancellation(cancellationToken))
            result.Add(account);
        return result;
    }

    private static string GetCurrencyDescription(CurrencyAccountEntry entry) =>
        !string.IsNullOrWhiteSpace(entry.Description) ? entry.Description : entry.ContractorDetails ?? string.Empty;

    private static string GetInvestmentDescription(InvestmentTransaction transaction) =>
        $"{transaction.Type} {transaction.Quantity:0.####} {transaction.AssetListing?.Ticker}".TrimEnd();

    private async Task<IReadOnlyDictionary<int, string?>> GetBondNames(
        IReadOnlyList<BondAccountEntry> entries,
        CancellationToken cancellationToken)
    {
        var missingIds = entries
            .Select(entry => entry.BondDetailsId)
            .Distinct()
            .Where(id => !_bondNamesCache.ContainsKey(id))
            .ToList();

        if (missingIds.Count != 0)
        {
            var names = await bondDetailsRepository.GetNamesByIdsAsync(missingIds, cancellationToken);
            foreach (var id in missingIds)
                _bondNamesCache[id] = names.TryGetValue(id, out var name) ? name : null;
        }

        return _bondNamesCache;
    }
}