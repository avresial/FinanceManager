using FinanceManager.Components.Features.FinancialAccounts.Components.Shared;
using FinanceManager.Components.Features.FinancialAccounts.Models;
using FinanceManager.Domain.FinancialAccounts.Investments.Dtos;
using FinanceManager.Domain.FinancialAccounts.Investments.Entities;

namespace FinanceManager.Components.Features.FinancialAccounts.Services;

/// <summary>
/// Maps the account history toolbar (range, Income/Expense chip, search) onto investment trades:
/// the server-side history query and the matching client-side filter for rows already loaded.
/// Income/Expense is the cash direction of a trade: a sell brings cash in, a buy takes it out.
/// </summary>
public static class InvestmentTransactionHistoryFilter
{
    public static InvestmentAccountHistoryQuery Unfiltered { get; } = new(null, null, null, null);

    public static InvestmentAccountHistoryQuery BuildQuery(
        DateTime dateStart,
        DateTime dateEnd,
        AccountHistoryToolbar.TxFilter? filter,
        string? searchText) => new(
            DateOnly.FromDateTime(dateStart),
            DateOnly.FromDateTime(dateEnd),
            filter switch
            {
                AccountHistoryToolbar.TxFilter.Income => InvestmentTransactionType.Sell,
                AccountHistoryToolbar.TxFilter.Expense => InvestmentTransactionType.Buy,
                _ => null
            },
            NormalizeSearch(searchText));

    public static bool IsUnfiltered(InvestmentAccountHistoryQuery query) =>
        query.StartDate is null
        && query.EndDate is null
        && query.Type is null
        && query.Search is null;

    /// <summary>
    /// Trades in [<paramref name="dateStart"/>, <paramref name="dateEnd"/>] matching the toolbar, newest
    /// first. The text search is applied locally only when <paramref name="serverQuery"/> did not already
    /// apply the same search.
    /// </summary>
    public static List<InvestmentTransactionDto> Apply(
        IEnumerable<InvestmentTransactionDto> transactions,
        DateTime dateStart,
        DateTime dateEnd,
        AccountHistoryToolbar.TxFilter? filter,
        string? searchText,
        InvestmentAccountHistoryQuery serverQuery)
    {
        var filtered = transactions
            .Where(t => t.TradeDate >= DateOnly.FromDateTime(dateStart) && t.TradeDate <= DateOnly.FromDateTime(dateEnd));

        if (filter == AccountHistoryToolbar.TxFilter.Income)
            filtered = filtered.Where(t => t.Type == InvestmentTransactionType.Sell);
        else if (filter == AccountHistoryToolbar.TxFilter.Expense)
            filtered = filtered.Where(t => t.Type == InvestmentTransactionType.Buy);

        var needle = NormalizeSearch(searchText);
        var serverSearchApplied = string.Equals(serverQuery.Search, needle, StringComparison.OrdinalIgnoreCase);
        if (!serverSearchApplied && needle is not null)
        {
            filtered = filtered.Where(t =>
                t.Ticker.Contains(needle, StringComparison.OrdinalIgnoreCase)
                || t.ExchangeName.Contains(needle, StringComparison.OrdinalIgnoreCase)
                || (t.Notes is not null && t.Notes.Contains(needle, StringComparison.OrdinalIgnoreCase)));
        }

        return [.. filtered
            .OrderByDescending(t => t.TradeDate)
            .ThenByDescending(t => t.Id)];
    }

    /// <summary>Signed cash effect of a trade: sale proceeds net of fee, or the negative purchase cost including fee.</summary>
    public static decimal CashImpact(InvestmentTransactionDto transaction)
    {
        var gross = transaction.Quantity * transaction.UnitPrice;
        var fee = transaction.Fee ?? 0m;
        return transaction.Type == InvestmentTransactionType.Sell ? gross - fee : -(gross + fee);
    }

    private static string? NormalizeSearch(string? searchText) =>
        string.IsNullOrWhiteSpace(searchText) ? null : searchText.Trim();
}