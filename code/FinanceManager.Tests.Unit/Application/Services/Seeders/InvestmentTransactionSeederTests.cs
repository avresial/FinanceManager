using FinanceManager.Application.Transactions.Investments.Seeders;
using FinanceManager.Domain.Assets.Entities;
using FinanceManager.Domain.Assets.Repositories;
using FinanceManager.Domain.Transactions.Investments.Entities;
using FinanceManager.Domain.Transactions.Investments.Repositories;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace FinanceManager.Tests.Unit.Application.Services.Seeders;

[Collection("Application")]
[Trait("Category", "Unit")]
public class InvestmentTransactionSeederTests
{
    private static readonly DateTime _start = new(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime _end = new(2025, 8, 1, 0, 0, 0, DateTimeKind.Utc);

    private readonly Mock<IInvestmentTransactionRepository> _transactions = new();
    private readonly Mock<IPriceQuoteRepository> _quotes = new();
    private readonly List<InvestmentTransaction> _saved = [];
    private readonly InvestmentTransactionSeeder _seeder;

    public InvestmentTransactionSeederTests()
    {
        Mock<IAssetRepository> assets = new();
        assets.Setup(r => r.Upsert(It.IsAny<Asset>(), It.IsAny<CancellationToken>())).ReturnsAsync(new Asset { Id = 3, Name = "A" });
        Mock<IAssetListingRepository> listings = new();
        listings.Setup(r => r.Upsert(It.IsAny<AssetListing>(), It.IsAny<CancellationToken>())).ReturnsAsync(new AssetListing { Id = 4, AssetId = 3 });
        Mock<IMarketDataSymbolRepository> symbols = new();
        _transactions.Setup(r => r.GetByUser(It.IsAny<long>(), It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);
        _transactions.Setup(r => r.Add(It.IsAny<InvestmentTransaction>(), It.IsAny<CancellationToken>()))
            .Callback<InvestmentTransaction, CancellationToken>((t, _) => _saved.Add(t))
            .ReturnsAsync((InvestmentTransaction t, CancellationToken _) => t);

        _seeder = new InvestmentTransactionSeeder(assets.Object, listings.Object, symbols.Object, _quotes.Object, _transactions.Object, NullLogger<InvestmentTransactionSeeder>.Instance);
    }

    [Fact]
    public async Task Seed_GuestUser_WritesOwnerScopedTradesWithoutAccount()
    {
        await _seeder.Seed(-9, _start, _end, new Random(1), TestContext.Current.CancellationToken);

        Assert.NotEmpty(_saved);
        Assert.All(_saved, t =>
        {
            Assert.Equal(-9, t.UserId);
            Assert.Equal(4, t.AssetListingId);
        });
        Assert.Contains(_saved, t => t.Type == InvestmentTransactionType.Buy);
        Assert.Single(_saved, t => t.Type == InvestmentTransactionType.Sell);
        _quotes.Verify(r => r.Add(It.IsAny<PriceQuote>(), It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }

    [Fact]
    public async Task Seed_UserIdZero_DoesNothing()
    {
        await _seeder.Seed(0, _start, _end, new Random(1), TestContext.Current.CancellationToken);

        Assert.Empty(_saved);
    }

    [Fact]
    public async Task Seed_UserWithExistingTrades_DoesNothing()
    {
        _transactions.Setup(r => r.GetByUser(5, It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>())).ReturnsAsync([new InvestmentTransaction()]);

        await _seeder.Seed(5, _start, _end, new Random(1), TestContext.Current.CancellationToken);

        Assert.Empty(_saved);
    }

    [Fact]
    public async Task Seed_SameRandomSeed_ProducesIdenticalTrades()
    {
        await _seeder.Seed(5, _start, _end, new Random(42), TestContext.Current.CancellationToken);
        var first = _saved.Select(t => (t.Type, t.Quantity, t.UnitPrice, t.TradeDate)).ToList();
        _saved.Clear();
        await _seeder.Seed(5, _start, _end, new Random(42), TestContext.Current.CancellationToken);

        Assert.Equal(first, _saved.Select(t => (t.Type, t.Quantity, t.UnitPrice, t.TradeDate)).ToList());
    }
}