using FinanceManager.Domain.FinancialAccounts.Bond.Entities;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.FinancialAccounts.Currencies.Repositories;
using FinanceManager.Domain.FinancialAccounts.Investments.Entities;
using FinanceManager.Domain.FinancialAccounts.Shared.Repositories;
using ModelContextProtocol.Server;
using System.ComponentModel;

namespace FinanceManager.Api.Features.Mcp.Tools;

[McpServerToolType]
public sealed class FinancialAccountTools(
    McpUserContext userContext,
    ICurrencyAccountRepository<CurrencyAccount> currencyAccounts,
    IAccountRepository<BondAccount> bondAccounts,
    IAccountRepository<InvestmentAccount> investmentAccounts)
{
    [McpServerTool(Name = "list_financial_accounts", ReadOnly = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true)]
    [Description("List financial accounts owned by the authenticated user. Never accepts or reads another user's id.")]
    public async Task<IReadOnlyList<McpFinancialAccount>> ListFinancialAccounts()
    {
        var userId = userContext.GetUserId();
        List<McpFinancialAccount> result = [];
        result.AddRange((await currencyAccounts.GetAll(userId)).Where(account => account.UserId == userId).Select(McpResponseMapper.Account));
        result.AddRange((await bondAccounts.GetAll(userId)).Where(account => account.UserId == userId).Select(McpResponseMapper.Account));
        result.AddRange((await investmentAccounts.GetAll(userId)).Where(account => account.UserId == userId).Select(McpResponseMapper.Account));
        return [.. result.OrderBy(account => account.Name, StringComparer.OrdinalIgnoreCase).ThenBy(account => account.AccountId)];
    }

    [McpServerTool(Name = "get_financial_account", ReadOnly = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true)]
    [Description("Get one financial account only when it belongs to the authenticated user. Returns null for missing or foreign accounts.")]
    public async Task<McpFinancialAccount?> GetFinancialAccount(
        [Description("Finance Manager account id returned by list_financial_accounts.")] int accountId)
    {
        var accounts = await ListFinancialAccounts();
        return accounts.SingleOrDefault(account => account.AccountId == accountId);
    }
}