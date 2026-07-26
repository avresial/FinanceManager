using FinanceManager.Domain.Assets.Repositories;
using FinanceManager.Domain.FinancialAccounts.Currencies.Repositories;
using FinanceManager.Domain.FinancialAccounts.Investments.Entities;
using FinanceManager.Domain.FinancialAccounts.Investments.Services;
using FinanceManager.Domain.FinancialAccounts.Shared.Repositories;
using ModelContextProtocol.Server;
using System.ComponentModel;

namespace FinanceManager.Api.Features.Mcp.Tools;

[McpServerToolType]
public sealed class InvestmentTools(
    McpUserContext userContext,
    IAccountRepository<InvestmentAccount> investmentAccounts,
    IInvestmentValuationService valuationService,
    ICurrencyRepository currencies,
    IAssetListingRepository assetListings)
{
    [McpServerTool(Name = "get_investment_portfolio", ReadOnly = true, Idempotent = true, OpenWorld = true, UseStructuredContent = true)]
    [Description("Return the authenticated user's investment positions and account values as of a date, using Finance Manager's valuation service.")]
    public async Task<McpInvestmentPortfolio> GetInvestmentPortfolio(
        [Description("Valuation date. Defaults to today.")] DateOnly? asOfDate = null,
        [Description("Target ISO currency code. Defaults to PLN.")] string currencyCode = "PLN",
        CancellationToken cancellationToken = default)
    {
        var userId = userContext.GetUserId();
        var asOf = asOfDate ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var currency = await currencies.GetByCode(currencyCode, cancellationToken)
            ?? throw new ArgumentException($"Unknown currency code '{currencyCode}'.", nameof(currencyCode));
        var accounts = (await investmentAccounts.GetAll(userId)).Where(account => account.UserId == userId).ToList();
        var accountMap = accounts.ToDictionary(account => account.AccountId);
        if (accounts.Count == 0)
            return new McpInvestmentPortfolio(asOf, currency.ShortName, 0, [], []);

        var values = await valuationService.GetAccountValueAsync(
            [.. accountMap.Keys], currency, asOf.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc), cancellationToken);
        List<McpInvestmentPosition> positions = [];
        foreach (var account in accounts)
        {
            var holdings = await valuationService.GetHoldingsAsOfAsync(account.AccountId, asOf, cancellationToken);
            foreach (var (listingId, quantity) in holdings)
            {
                var listing = await assetListings.Get(listingId, cancellationToken);
                if (listing is not null)
                    positions.Add(McpResponseMapper.InvestmentPosition(account.AccountId, account.Name, listing, quantity));
            }
        }
        positions = [.. positions
            .OrderBy(position => position.AccountName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(position => position.Ticker, StringComparer.OrdinalIgnoreCase)];
        var accountValues = accounts
            .Select(account => new McpInvestmentAccountValue(
                account.AccountId,
                account.Name,
                values.GetValueOrDefault(account.AccountId)))
            .OrderBy(account => account.AccountName, StringComparer.OrdinalIgnoreCase)
            .ToList();
        return new McpInvestmentPortfolio(asOf, currency.ShortName, accountValues.Sum(account => account.Value), accountValues, positions);
    }
}