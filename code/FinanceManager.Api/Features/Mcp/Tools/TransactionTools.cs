using FinanceManager.Domain.Dashboard.Dtos;
using FinanceManager.Domain.Dashboard.Services;
using FinanceManager.Domain.FinancialAccounts.Investments.Entities;
using FinanceManager.Domain.FinancialAccounts.Investments.Repositories;
using FinanceManager.Domain.Transactions.Bonds.Dtos;
using FinanceManager.Domain.Transactions.Bonds.Repositories;
using FinanceManager.Domain.Transactions.Cash.Dtos;
using FinanceManager.Domain.Transactions.Cash.Repositories;
using ModelContextProtocol.Server;
using System.ComponentModel;

namespace FinanceManager.Api.Features.Mcp.Tools;

[McpServerToolType]
public sealed class TransactionTools(
    McpUserContext userContext,
    ITransactionLogService transactionLog,
    ICashTransactionReader cashTransactions,
    IBondTransactionReader bondTransactions,
    IInvestmentTransactionRepository investmentTransactions)
{
    private const int _pageSize = 100;
    private const int _maxScannedTransactions = 5000;

    [McpServerTool(Name = "list_transactions", ReadOnly = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true)]
    [Description("List the authenticated user's cash, bond and investment transactions with optional date, kind, currency, label, text and paging filters. " +
        "Newest first (by posting time, then id, then kind). Defaults to the last 90 days. Scans at most 5000 transactions of the range per call; narrow the dates to reach older rows.")]
    public async Task<McpTransactionPage> ListTransactions(
        [Description("Inclusive start date. Defaults to 90 days before endDate.")] DateOnly? startDate = null,
        [Description("Inclusive end date. Defaults to today (UTC).")] DateOnly? endDate = null,
        [Description("Optional kind: cash, bond or investment.")] string? kind = null,
        [Description("Optional ISO currency code, case-insensitive.")] string? currency = null,
        [Description("Optional exact label/category name, case-insensitive.")] string? label = null,
        [Description("Optional text contained in the description, bond name, ticker or a label.")] string? query = null,
        [Description("Number of matching rows to skip, from 0.")] int offset = 0,
        [Description("Page size from 1 to 100.")] int limit = 50,
        CancellationToken cancellationToken = default)
    {
        if (offset < 0) throw new ArgumentOutOfRangeException(nameof(offset), "Offset cannot be negative.");
        if (limit is < 1 or > 100) throw new ArgumentOutOfRangeException(nameof(limit), "Limit must be between 1 and 100.");

        var end = endDate ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var start = startDate ?? end.AddDays(-90);
        if (start > end) throw new ArgumentException("Start date cannot be after end date.");
        var kinds = kind is null ? null : new[] { ParseKind(kind) };

        var userId = userContext.GetUserId();
        // The search text is matched here, on description and labels only, and is never handed to the readers: their SQL search also covers the private cash counterparty.
        var logQuery = new TransactionLogQuery(
            start.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
            end.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc),
            _pageSize,
            Kinds: kinds,
            Currencies: string.IsNullOrWhiteSpace(currency) ? null : [currency]);

        // Enough matches to fill the page and prove whether another one follows; long arithmetic cannot overflow for any offset.
        var wanted = (long)offset + limit + 1;
        List<McpTransaction> matches = [];
        var scanned = 0;
        while (matches.Count < wanted && scanned < _maxScannedTransactions)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var page = await transactionLog.GetItemsPage(userId, logQuery, cancellationToken);
            foreach (var item in page.Items)
            {
                scanned++;
                if (ToTransaction(item) is not McpTransaction transaction) continue;
                if (!string.IsNullOrWhiteSpace(label) && !transaction.Labels.Contains(label.Trim(), StringComparer.OrdinalIgnoreCase)) continue;
                if (!MatchesQuery(transaction, query)) continue;
                matches.Add(transaction);
            }

            if (page.NextCursor is null) break;
            logQuery = logQuery with { Cursor = page.NextCursor };
        }

        var rows = matches.Skip(offset).Take(limit).ToList();
        return new McpTransactionPage(rows, offset, limit, matches.Count > offset + rows.Count);
    }

    [McpServerTool(Name = "get_transaction", ReadOnly = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true)]
    [Description("Get one transaction by kind and id, only when it belongs to the authenticated user. Returns null for missing or foreign data.")]
    public async Task<McpTransaction?> GetTransaction(
        [Description("Transaction kind: cash, bond or investment.")] string kind,
        [Description("Transaction id returned by list_transactions; ids are unique per kind.")] long transactionId,
        CancellationToken cancellationToken = default)
    {
        var parsedKind = ParseKind(kind);
        var userId = userContext.GetUserId();
        if (transactionId <= 0) return null;

        switch (parsedKind)
        {
            case TransactionKind.Cash:
                return (await cashTransactions.GetByIds(userId, [transactionId], cancellationToken)).SingleOrDefault() is CashLogEntry cash
                    ? McpResponseMapper.Transaction(cash)
                    : null;
            case TransactionKind.Bond:
                return await bondTransactions.GetById(userId, transactionId, cancellationToken) is BondLogEntry bond
                    ? McpResponseMapper.Transaction(bond)
                    : null;
            default:
                return await investmentTransactions.Get(transactionId, cancellationToken) is InvestmentTransaction trade && trade.UserId == userId
                    ? McpResponseMapper.Transaction(trade)
                    : null;
        }
    }

    private static TransactionKind ParseKind(string? kind) => (kind ?? throw new ArgumentException("Kind must be cash, bond, or investment.", nameof(kind))).Trim().ToLowerInvariant() switch
    {
        "cash" => TransactionKind.Cash,
        "bond" => TransactionKind.Bond,
        "investment" => TransactionKind.Investment,
        _ => throw new ArgumentException("Kind must be cash, bond, or investment.", nameof(kind))
    };

    private static McpTransaction? ToTransaction(TransactionLogItem item) => item switch
    {
        CurrencyTransactionLogItem { Entry: var cash } => McpResponseMapper.Transaction(cash),
        BondTransactionLogItem { Entry: var bond } => McpResponseMapper.Transaction(bond),
        InvestmentTransactionLogItem { Transaction: var trade } => McpResponseMapper.Transaction(trade),
        _ => null
    };

    private static bool MatchesQuery(McpTransaction transaction, string? query) =>
        string.IsNullOrWhiteSpace(query) ||
        transaction.Description.Contains(query.Trim(), StringComparison.OrdinalIgnoreCase) ||
        transaction.Labels.Any(label => label.Contains(query.Trim(), StringComparison.OrdinalIgnoreCase));
}