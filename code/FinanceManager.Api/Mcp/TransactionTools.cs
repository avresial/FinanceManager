using FinanceManager.Domain.FinancialAccounts.Bond.Entities;
using FinanceManager.Domain.FinancialAccounts.Bond.Repositories;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.FinancialAccounts.Currencies.Repositories;
using FinanceManager.Domain.FinancialAccounts.Investments.Entities;
using FinanceManager.Domain.FinancialAccounts.Investments.Repositories;
using FinanceManager.Domain.FinancialAccounts.Shared.Repositories;
using ModelContextProtocol.Server;
using System.ComponentModel;

namespace FinanceManager.Api.Mcp;

[McpServerToolType]
public sealed class TransactionTools(
    McpUserContext userContext,
    ICurrencyAccountRepository<CurrencyAccount> currencyAccounts,
    IAccountRepository<BondAccount> bondAccounts,
    IAccountRepository<InvestmentAccount> investmentAccounts,
    IAccountEntryRepository<CurrencyAccountEntry> currencyEntries,
    IBondAccountEntryRepository<BondAccountEntry> bondEntries,
    IInvestmentTransactionRepository investmentTransactions,
    IBondDetailsRepository bondDetails)
{
    [McpServerTool(Name = "list_transactions", ReadOnly = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true)]
    [Description("List the authenticated user's transactions with optional date, account, label, text, and paging filters. Defaults to the last 90 days.")]
    public async Task<McpTransactionPage> ListTransactions(
        [Description("Inclusive start date. Defaults to 90 days before endDate.")] DateOnly? startDate = null,
        [Description("Inclusive end date. Defaults to today.")] DateOnly? endDate = null,
        [Description("Optional owned account id.")] int? accountId = null,
        [Description("Optional exact label/category name, case-insensitive.")] string? label = null,
        [Description("Optional text contained in description, account name, or ticker.")] string? query = null,
        [Description("Number of matching rows to skip, from 0.")] int offset = 0,
        [Description("Page size from 1 to 100.")] int limit = 50,
        CancellationToken cancellationToken = default)
    {
        if (offset < 0) throw new ArgumentOutOfRangeException(nameof(offset), "Offset cannot be negative.");
        if (limit is < 1 or > 100) throw new ArgumentOutOfRangeException(nameof(limit), "Limit must be between 1 and 100.");

        var end = endDate ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var start = startDate ?? end.AddDays(-90);
        if (start > end) throw new ArgumentException("Start date cannot be after end date.");

        var userId = userContext.GetUserId();
        var currencyAccountMap = (await currencyAccounts.GetAll(userId)).Where(account => account.UserId == userId).ToDictionary(account => account.AccountId);
        var bondAccountMap = (await bondAccounts.GetAll(userId)).Where(account => account.UserId == userId).ToDictionary(account => account.AccountId);
        var investmentAccountMap = (await investmentAccounts.GetAll(userId)).Where(account => account.UserId == userId).ToDictionary(account => account.AccountId);
        var ownedAccountIds = currencyAccountMap.Keys.Concat(bondAccountMap.Keys).Concat(investmentAccountMap.Keys).ToHashSet();
        if (accountId is not null && !ownedAccountIds.Contains(accountId.Value))
            return new McpTransactionPage([], offset, limit, false);

        List<McpTransaction> transactions = [];
        var rangeStart = start.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var rangeEnd = end.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc);
        var currencyIds = SelectedIds(currencyAccountMap.Keys, accountId);
        if (currencyIds.Count != 0)
        {
            transactions.AddRange((await currencyEntries.GetRange(currencyIds, rangeStart, rangeEnd))
                .Where(entry => currencyAccountMap.ContainsKey(entry.AccountId))
                .Select(entry => McpResponseMapper.Transaction(entry, currencyAccountMap[entry.AccountId].Name)));
        }

        var bondIds = SelectedIds(bondAccountMap.Keys, accountId);
        if (bondIds.Count != 0)
        {
            var bondNames = new Dictionary<int, string>();
            foreach (var entry in await bondEntries.GetRange(bondIds, rangeStart, rangeEnd))
            {
                if (!bondAccountMap.ContainsKey(entry.AccountId)) continue;
                if (!bondNames.TryGetValue(entry.BondDetailsId, out var bondName))
                {
                    bondName = (await bondDetails.GetByIdAsync(entry.BondDetailsId, cancellationToken))?.Name ?? "Bond";
                    bondNames[entry.BondDetailsId] = bondName;
                }
                transactions.Add(McpResponseMapper.Transaction(entry, bondAccountMap[entry.AccountId].Name, bondName));
            }
        }

        var investments = await investmentTransactions.GetByUser(userId, start, end, cancellationToken);
        transactions.AddRange(investments
            .Where(transaction => transaction.UserId == userId)
            .Where(transaction => accountId is null || transaction.AccountId == accountId)
            .Where(transaction => investmentAccountMap.ContainsKey(transaction.AccountId))
            .Select(transaction => McpResponseMapper.Transaction(transaction, investmentAccountMap[transaction.AccountId].Name)));

        var filtered = transactions
            .Where(transaction => string.IsNullOrWhiteSpace(label) || transaction.Labels.Contains(label, StringComparer.OrdinalIgnoreCase))
            .Where(transaction => MatchesQuery(transaction, query))
            .OrderByDescending(transaction => transaction.Date)
            .ThenByDescending(transaction => transaction.TransactionId)
            .ToList();
        var page = filtered.Skip(offset).Take(limit).ToList();
        return new McpTransactionPage(page, offset, limit, filtered.Count > offset + page.Count);
    }

    [McpServerTool(Name = "get_transaction", ReadOnly = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true)]
    [Description("Get transaction details only when the account and transaction belong to the authenticated user. Returns null for missing or foreign data.")]
    public async Task<McpTransaction?> GetTransaction(
        [Description("Account type: currency, bond, or investment.")] string accountType,
        [Description("Owned account id.")] int accountId,
        [Description("Transaction id returned by list_transactions.")] long transactionId,
        CancellationToken cancellationToken = default)
    {
        var userId = userContext.GetUserId();
        switch (accountType.Trim().ToLowerInvariant())
        {
            case "currency":
                {
                    var account = (await currencyAccounts.GetAll(userId)).SingleOrDefault(item => item.UserId == userId && item.AccountId == accountId);
                    if (account is null || transactionId is < int.MinValue or > int.MaxValue) return null;
                    var entry = await currencyEntries.Get(accountId, (int)transactionId);
                    return entry is null || entry.AccountId != accountId
                        ? null
                        : McpResponseMapper.Transaction(entry, account.Name);
                }
            case "bond":
                {
                    var account = (await bondAccounts.GetAll(userId)).SingleOrDefault(item => item.UserId == userId && item.AccountId == accountId);
                    if (account is null || transactionId is < int.MinValue or > int.MaxValue) return null;
                    var entry = await bondEntries.Get(accountId, (int)transactionId);
                    if (entry is null || entry.AccountId != accountId) return null;
                    var name = (await bondDetails.GetByIdAsync(entry.BondDetailsId, cancellationToken))?.Name ?? "Bond";
                    return McpResponseMapper.Transaction(entry, account.Name, name);
                }
            case "investment":
                {
                    var account = (await investmentAccounts.GetAll(userId)).SingleOrDefault(item => item.UserId == userId && item.AccountId == accountId);
                    if (account is null) return null;
                    var transaction = await investmentTransactions.Get(transactionId, cancellationToken);
                    return transaction is null || transaction.UserId != userId || transaction.AccountId != accountId
                        ? null
                        : McpResponseMapper.Transaction(transaction, account.Name);
                }
            default:
                throw new ArgumentException("Account type must be currency, bond, or investment.", nameof(accountType));
        }
    }

    private static IReadOnlyCollection<int> SelectedIds(IEnumerable<int> accountIds, int? selectedAccountId) =>
        [.. accountIds.Where(id => selectedAccountId is null || id == selectedAccountId)];

    private static bool MatchesQuery(McpTransaction transaction, string? query) =>
        string.IsNullOrWhiteSpace(query) ||
        transaction.Description.Contains(query, StringComparison.OrdinalIgnoreCase) ||
        transaction.AccountName.Contains(query, StringComparison.OrdinalIgnoreCase) ||
        transaction.Labels.Any(label => label.Contains(query, StringComparison.OrdinalIgnoreCase));
}