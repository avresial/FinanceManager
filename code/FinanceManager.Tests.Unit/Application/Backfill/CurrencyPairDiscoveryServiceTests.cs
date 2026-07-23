using FinanceManager.Application.Backfill.Currencies;
using FinanceManager.Domain.Assets.Entities;
using FinanceManager.Domain.Assets.Repositories;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.FinancialAccounts.Currencies.Repositories;
using FinanceManager.Domain.FinancialAccounts.Investments.Repositories;
using FinanceManager.Domain.Identity.Repositories;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace FinanceManager.Tests.Unit.Application.Backfill;

[Trait("Category", "Unit")]
public class CurrencyPairDiscoveryServiceTests
{
    private readonly Mock<IInvestmentTransactionRepository> _transactions = new();
    private readonly Mock<IAssetListingRepository> _listings = new();
    private readonly Mock<IUserRepository> _users = new();
    private readonly Mock<ICurrencyRepository> _currencies = new();

    private CurrencyPairDiscoveryService CreateSut() => new(
        _transactions.Object,
        _listings.Object,
        _users.Object,
        _currencies.Object,
        NullLogger<CurrencyPairDiscoveryService>.Instance);

    [Fact]
    public async Task BuildsNormalizedCrossProductOfTradedAndPreferredCurrencies()
    {
        _transactions.Setup(x => x.GetDistinctAssetListingIds(It.IsAny<CancellationToken>())).ReturnsAsync([1L, 2L]);
        // GBX normalises to GBP; "usd" normalises to USD.
        _listings.Setup(x => x.Get(1L, It.IsAny<CancellationToken>())).ReturnsAsync(new AssetListing { TradingCurrency = "GBX" });
        _listings.Setup(x => x.Get(2L, It.IsAny<CancellationToken>())).ReturnsAsync(new AssetListing { TradingCurrency = "usd" });

        _users.Setup(x => x.GetDistinctPreferredCurrencyIds(It.IsAny<CancellationToken>())).ReturnsAsync([0, 1]);
        _currencies.Setup(x => x.GetCurrency(0, It.IsAny<CancellationToken>())).ReturnsAsync(DefaultCurrency.PLN);
        _currencies.Setup(x => x.GetCurrency(1, It.IsAny<CancellationToken>())).ReturnsAsync(DefaultCurrency.USD);

        var pairs = await CreateSut().GetPairsAsync(TestContext.Current.CancellationToken);

        // Sources {GBP, USD} × targets {PLN, USD}, excluding USD→USD.
        Assert.Equal(
            new HashSet<CurrencyPair>
            {
                new("GBP", "PLN"),
                new("GBP", "USD"),
                new("USD", "PLN"),
            },
            pairs.ToHashSet());
    }

    [Fact]
    public async Task NoListingsOrUsers_ReturnsNoPairs()
    {
        _transactions.Setup(x => x.GetDistinctAssetListingIds(It.IsAny<CancellationToken>())).ReturnsAsync([]);
        _users.Setup(x => x.GetDistinctPreferredCurrencyIds(It.IsAny<CancellationToken>())).ReturnsAsync([]);

        var pairs = await CreateSut().GetPairsAsync(TestContext.Current.CancellationToken);

        Assert.Empty(pairs);
    }
}