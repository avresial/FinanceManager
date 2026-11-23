using FinanceManager.Domain.Assets.Repositories;
using FinanceManager.Domain.FinancialAccounts.Currencies.Repositories;
using FinanceManager.Domain.FinancialAccounts.Investments.Repositories;
using FinanceManager.Domain.FinancialAccounts.Investments.Services;
using ModelContextProtocol.Server;
using System.ComponentModel;

namespace FinanceManager.Api.Features.Mcp.Tools;

[McpServerToolType]
public sealed class InvestmentTools(
    McpUserContext userContext,
    IInvestmentTransactionRepository transactions,
    IInvestmentValuationService valuationService,
    ICurrencyRepository currencies,
    IAssetListingRepository assetListings)
{
    [McpServerTool(Name = "get_investment_portfolio", ReadOnly = true, Idempotent = true, OpenWorld = true, UseStructuredContent = true)]
    [Description("Return the authenticated user's pooled investment positions (quantity and value per listing) and their total value as of a date, using Finance Manager's valuation service.")]
    public async Task<McpInvestmentPortfolio> GetInvestmentPortfolio(
        [Description("Valuation date. Defaults to today.")] DateOnly? asOfDate = null,
        [Description("Target ISO currency code. Defaults to PLN.")] string currencyCode = "PLN",
        CancellationToken cancellationToken = default)
    {
        var userId = userContext.GetUserId();
        var asOf = asOfDate ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var currency = await currencies.GetByCode(currencyCode, cancellationToken)
            ?? throw new ArgumentException($"Unknown currency code '{currencyCode}'.", nameof(currencyCode));

        var holdings = await transactions.GetHoldingsByUserAsOf(userId, asOf, cancellationToken);
        if (holdings.Count == 0)
            return new McpInvestmentPortfolio(asOf, currency.ShortName, 0, []);

        var values = await valuationService.GetUserValueByListingAsync(
            userId, currency, asOf.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc), cancellationToken);
        var listings = (await assetListings.GetByIds([.. holdings.Select(holding => holding.AssetListingId)], cancellationToken))
            .ToDictionary(listing => listing.Id);

        var positions = holdings
            .Where(holding => listings.ContainsKey(holding.AssetListingId))
            .Select(holding => McpResponseMapper.InvestmentPosition(
                listings[holding.AssetListingId], holding.Units, values.GetValueOrDefault(holding.AssetListingId)))
            .OrderBy(position => position.Ticker, StringComparer.OrdinalIgnoreCase)
            .ThenBy(position => position.Exchange, StringComparer.OrdinalIgnoreCase)
            .ThenBy(position => position.AssetListingId)
            .ToList();

        // The total is the pooled valuation itself, so it also covers a listing whose catalogue row is missing.
        return new McpInvestmentPortfolio(asOf, currency.ShortName, values.Values.Sum(), positions);
    }
}