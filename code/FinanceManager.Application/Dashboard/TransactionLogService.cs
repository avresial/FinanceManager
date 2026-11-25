using FinanceManager.Domain.Currencies.Entities;
using FinanceManager.Domain.Dashboard.Dtos;
using FinanceManager.Domain.Dashboard.Services;
using FinanceManager.Domain.Portfolio.Dividends.Repositories;
using FinanceManager.Domain.Transactions.Bonds.Repositories;
using FinanceManager.Domain.Transactions.Cash.Repositories;
using FinanceManager.Domain.Transactions.Investments.Repositories;
using FinanceManager.Domain.Transactions.Shared.Dtos;

namespace FinanceManager.Application.Dashboard;

public class TransactionLogService(ICashTransactionReader cash, IBondTransactionReader bonds,
    IInvestmentTransactionRepository investments, IDividendPaymentRepository dividends) : ITransactionLogService
{
    public async Task<bool> HasAny(int userId, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfZero(userId);
        // The probes share a scoped DbContext, so they run one after another and stop at the first hit.
        return await cash.HasAnyEntries(userId, cancellationToken)
            || await bonds.HasAnyEntries(userId, cancellationToken)
            || await investments.HasAnyByUser(userId, cancellationToken)
            || await dividends.HasPayments(userId, cancellationToken);
    }

    public async Task<List<TransactionLogEntryDto>> GetLastTransactions(int userId, int count, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(count, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(count, 100);
        var rows = await Read(userId, null, count, cancellationToken);
        return [.. rows.Take(count).Select(row => row.Summary)];
    }

    public async Task<TransactionLogItemsPage> GetItemsPage(int userId, TransactionLogQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(query.PageSize, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(query.PageSize, 100);
        query = query with
        {
            Start = TransactionLogQuery.AsUtc(query.Start),
            End = TransactionLogQuery.AsUtc(query.End),
            Cursor = query.Cursor is TransactionLogCursor cursor ? cursor with { Date = TransactionLogQuery.AsUtc(cursor.Date) } : null
        };
        if (query.End < query.Start) throw new ArgumentException("End precedes start.", nameof(query));
        if (query.Cursor is TransactionLogCursor boundary && (!Enum.IsDefined(boundary.Kind) || boundary.EntryId <= 0))
            throw new ArgumentException("Invalid cursor.", nameof(query));

        var rows = await Read(userId, query, query.PageSize + 1, cancellationToken);
        var page = rows.Take(query.PageSize).ToList();
        TransactionLogCursor? next = null;
        if (rows.Count > page.Count)
        {
            var last = page[^1].Summary;
            next = new(last.Date, last.Kind, last.EntryId);
        }
        return new([.. page.Select(row => row.Item)], next);
    }

    public async Task<List<string>> GetCurrencies(int userId, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfZero(userId);
        // The streams share a scoped DbContext, so they run one after another.
        var codes = new List<string>();
        codes.AddRange(await cash.GetCurrencies(userId, cancellationToken));
        codes.AddRange(await bonds.GetCurrencies(userId, cancellationToken));
        codes.AddRange(await investments.GetCurrenciesByUser(userId, cancellationToken));
        // Quote units such as GBX are a price convention, not something a user holds, so they are not offered.
        return [.. codes.Select(code => code.Trim().ToUpperInvariant())
            .Where(code => code.Length == 3 && code.All(char.IsAsciiLetter) && !DefaultCurrency.MinorQuoteUnits.ContainsKey(code))
            .Distinct().Order()];
    }

    private async Task<List<LogRow>> Read(int userId, TransactionLogQuery? query, int take, CancellationToken token)
    {
        ArgumentOutOfRangeException.ThrowIfZero(userId);
        token.ThrowIfCancellationRequested();
        var end = query?.End;
        if (query?.Cursor is TransactionLogCursor cursor && cursor.Date < end) end = cursor.Date;
        if (end < query?.Start) return [];
        bool Includes(TransactionKind kind) => query?.Kinds is not { Count: > 0 } kinds || kinds.Contains(kind);
        // The dashboard summary needs a cash entry's first label as its caption; only the full list loads the heavier investment details.
        OwnerHistoryQuery History(TransactionKind kind) => new(query?.Start, end, take,
            query?.Cursor is TransactionLogCursor c ? new(c.Date, c.EntryId, kind > c.Kind) : null,
            query?.Currencies, query?.Search, IncludeDetails: query is not null || kind == TransactionKind.Cash);

        // The streams share a scoped DbContext and must finish buffering one at a time.
        List<LogRow> rows = [];
        if (Includes(TransactionKind.Cash))
            foreach (var e in await cash.GetHistory(userId, History(TransactionKind.Cash), token))
                rows.Add(new(new(TransactionKind.Cash, e.EntryId, e.PostingDate, e.ValueChange,
                    string.IsNullOrWhiteSpace(e.Description) ? e.ContractorDetails ?? string.Empty : e.Description, e.Currency,
                    e.Labels.FirstOrDefault()?.Name),
                    new CurrencyTransactionLogItem(e)));
        if (Includes(TransactionKind.Bond))
            foreach (var e in await bonds.GetHistory(userId, History(TransactionKind.Bond), token))
                rows.Add(new(new(TransactionKind.Bond, e.EntryId, e.PostingDate, e.ValueChange,
                    string.IsNullOrWhiteSpace(e.Description) ? (string.IsNullOrWhiteSpace(e.BondName) ? "Bond" : e.BondName) : e.Description, "units", e.BondName),
                    new BondTransactionLogItem(e)));
        if (Includes(TransactionKind.Investment))
            foreach (var t in await investments.GetHistoryByUser(userId, History(TransactionKind.Investment), token))
                rows.Add(new(new(TransactionKind.Investment, t.EntryId, t.PostingDate, t.SignedQuantity * t.UnitPrice,
                    $"{t.Type} {t.Quantity:0.####} {t.AssetListing?.Ticker}".TrimEnd(), t.Currency, t.AssetListing?.Ticker), new InvestmentTransactionLogItem(t)));

        return [.. rows.OrderByDescending(r => r.Summary.Date).ThenByDescending(r => r.Summary.EntryId).ThenBy(r => r.Summary.Kind)];
    }

    private sealed record LogRow(TransactionLogEntryDto Summary, TransactionLogItem Item);
}