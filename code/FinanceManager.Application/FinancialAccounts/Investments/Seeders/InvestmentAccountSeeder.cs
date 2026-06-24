using FinanceManager.Application.Identity.Seeders;
using FinanceManager.Domain.Assets.Entities;
using FinanceManager.Domain.Assets.Repositories;
using FinanceManager.Domain.FinancialAccounts.Investments.Entities;
using FinanceManager.Domain.FinancialAccounts.Investments.Repositories;
using FinanceManager.Domain.FinancialAccounts.Shared.Repositories;
using Microsoft.Extensions.Logging;

namespace FinanceManager.Application.FinancialAccounts.Investments.Seeders;

/// <summary>
/// Seeds a demo investment account on the new asset model for the guest user: an iShares Core S&amp;P 500
/// asset with a single primary listing, an AlphaVantage market-data symbol, a daily price-quote series,
/// and monthly Buy transactions (plus one Sell) so the new investment UI renders a life-like portfolio.
/// </summary>
public class InvestmentAccountSeeder(
    IAccountRepository<InvestmentAccount> accountRepository,
    IAssetRepository assetRepository,
    IAssetListingRepository assetListingRepository,
    IMarketDataSymbolRepository marketDataSymbolRepository,
    IPriceQuoteRepository priceQuoteRepository,
    IInvestmentTransactionRepository transactionRepository,
    ILogger<InvestmentAccountSeeder> logger)
{
    private const string _isin = "IE00B5BMR087";
    private const string _ticker = "CSPX";
    private const string _exchangeMic = "XLON";
    private const string _exchangeName = "London Stock Exchange";
    private const string _currency = "USD";

    public async Task Seed(long userId, DateTime start, DateTime end, CancellationToken cancellationToken = default)
    {
        if (userId is > int.MaxValue or < int.MinValue)
        {
            logger.LogWarning("Skipping investment account seed: user id {UserId} is outside Int32 range.", userId);
            return;
        }

        var userIdInt = (int)userId;
        if ((await transactionRepository.GetByUser(userId, DateOnly.FromDateTime(start.AddYears(-1)), DateOnly.FromDateTime(end.AddYears(1)), cancellationToken)).Count != 0)
            return;

        logger.LogTrace("Seeding investment account for user {UserId}.", userId);

        var accountId = await accountRepository.Add(userIdInt, "Investments");
        if (accountId is not int newAccountId)
        {
            logger.LogWarning("Could not create investment account for user {UserId}; skipping seed.", userId);
            return;
        }

        var listing = await SeedAssetAndListing(cancellationToken);
        var prices = await SeedPriceQuotes(listing.Id, start, end, cancellationToken);

        await SeedTransactions(userId, newAccountId, listing.Id, start, end, prices, cancellationToken);
    }

    private async Task<AssetListing> SeedAssetAndListing(CancellationToken cancellationToken)
    {
        var asset = await assetRepository.Upsert(new Asset
        {
            Name = "iShares Core S&P 500 UCITS ETF",
            Type = AssetType.ETF,
            Isin = _isin,
            Issuer = "iShares",
            DistributionPolicy = DistributionPolicy.Accumulating,
            IsUcits = true,
            Identifiers =
            [
                new AssetIdentifier { Type = AssetIdentifierType.ISIN, Value = _isin, Scope = IdentifierScope.Asset, Source = nameof(InvestmentAccountSeeder), IsPrimary = true }
            ]
        }, cancellationToken);

        var listing = await assetListingRepository.Upsert(new AssetListing
        {
            AssetId = asset.Id,
            Ticker = _ticker,
            ExchangeMic = _exchangeMic,
            ExchangeName = _exchangeName,
            TradingCurrency = _currency,
            IsPrimaryListing = true,
            PriceMultiplier = 1m,
            IsActive = true
        }, cancellationToken);

        await marketDataSymbolRepository.Upsert(new MarketDataSymbol
        {
            AssetListingId = listing.Id,
            Provider = MarketDataProvider.AlphaVantage,
            Symbol = "CSPX.LON",
            Currency = _currency,
            IsPrimary = true,
            IsEnabled = true
        }, cancellationToken);

        return listing;
    }

    private async Task<Dictionary<DateTime, decimal>> SeedPriceQuotes(long listingId, DateTime start, DateTime end, CancellationToken cancellationToken)
    {
        // Random-walk price seed near a plausible CSPX value so the UI shows life-like valuations.
        var prices = new Dictionary<DateTime, decimal>();
        decimal price = 400m;
        for (var date = start.Date; date <= end.Date; date = date.AddDays(1))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var driftPercent = (decimal)((Random.Shared.NextDouble() - 0.48) * 0.04);
            price = Math.Clamp(price * (1 + driftPercent), 250m, 600m);
            price = Math.Round(price, 2);
            prices[date] = price;

            await priceQuoteRepository.Add(new PriceQuote
            {
                AssetListingId = listingId,
                Provider = MarketDataProvider.AlphaVantage,
                Price = price,
                Currency = _currency,
                PriceTime = new DateTimeOffset(date.Date, TimeSpan.Zero),
                QuoteType = PriceQuoteType.EndOfDay,
                FetchedAt = DateTimeOffset.UtcNow
            }, cancellationToken);
        }

        return prices;
    }

    private async Task SeedTransactions(long userId, int accountId, long listingId, DateTime start, DateTime end, IReadOnlyDictionary<DateTime, decimal> prices, CancellationToken cancellationToken)
    {
        // Mirror the cash account's recurring "Investment" outflow: buy the matching value of shares each month
        // at that day's price, then sell a small slice near the end so the history shows both directions.
        DateOnly? lastBuyDate = null;
        for (var date = start.AddDays(1); date <= end; date = date.AddDays(1))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!GuestInvestmentPlan.IsStockInvestmentDay(date)) continue;
            if (!prices.TryGetValue(date.Date, out var price) || price <= 0) continue;

            var units = Math.Round(GuestInvestmentPlan.StockMonthlyAmount / price, 4, MidpointRounding.AwayFromZero);
            if (units <= 0) continue;

            await transactionRepository.Add(new InvestmentTransaction
            {
                UserId = userId,
                AccountId = accountId,
                AssetListingId = listingId,
                Type = InvestmentTransactionType.Buy,
                Quantity = units,
                UnitPrice = price,
                Currency = _currency,
                TradeDate = DateOnly.FromDateTime(date)
            }, cancellationToken);
            lastBuyDate = DateOnly.FromDateTime(date);
        }

        // A single Sell a few days after the last buy keeps a positive net holding while exercising both directions.
        if (lastBuyDate is DateOnly buyDate)
        {
            var sellDate = buyDate.AddDays(5);
            var sellPrice = prices.TryGetValue(sellDate.ToDateTime(TimeOnly.MinValue), out var p) ? p : prices[end.Date];
            await transactionRepository.Add(new InvestmentTransaction
            {
                UserId = userId,
                AccountId = accountId,
                AssetListingId = listingId,
                Type = InvestmentTransactionType.Sell,
                Quantity = 0.25m,
                UnitPrice = sellPrice,
                Currency = _currency,
                TradeDate = sellDate <= DateOnly.FromDateTime(end) ? sellDate : DateOnly.FromDateTime(end)
            }, cancellationToken);
        }
    }
}