using FinanceManager.Application.FinancialAccounts.Investments.Discovery;
using FinanceManager.Domain.Assets.Discovery;
using FinanceManager.Domain.Assets.Dtos;
using FinanceManager.Domain.Assets.Entities;
using FinanceManager.Domain.Assets.Repositories;
using Moq;

namespace FinanceManager.Tests.Unit.Application.Services.Discovery;

[Trait("Category", "Unit")]
public class IncompleteAssetQueueServiceTests
{
    private readonly Mock<IAssetRepository> _assets = new();
    private readonly Mock<IAssetListingRepository> _listings = new();
    private readonly Mock<IMarketDataSymbolRepository> _symbols = new();
    private readonly Mock<IMarketDataSymbolResolver> _resolver = new();
    private readonly AssetListing _listing = new() { Id = 4, ExchangeMic = "XWAR", TradingCurrency = "PLN" };
    private readonly MarketDataSymbolDto _draft = new() { Provider = MarketDataProvider.AlphaVantage, Symbol = " TEST " };

    public IncompleteAssetQueueServiceTests()
    {
        _listings.Setup(x => x.Get(4, It.IsAny<CancellationToken>())).ReturnsAsync(_listing);
        _resolver.SetupGet(x => x.Provider).Returns(MarketDataProvider.AlphaVantage);
        _resolver.Setup(x => x.ValidateAsync(It.IsAny<MarketDataSymbolResolution>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _symbols.Setup(x => x.Add(It.IsAny<MarketDataSymbol>(), It.IsAny<CancellationToken>())).ReturnsAsync((MarketDataSymbol s, CancellationToken _) => s);
        _symbols.Setup(x => x.Update(It.IsAny<MarketDataSymbol>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _symbols.Setup(x => x.RecordFetchResult(It.IsAny<long>(), It.IsAny<DateTimeOffset?>(), null, It.IsAny<CancellationToken>())).ReturnsAsync(true);
    }

    private IncompleteAssetQueueService Service() => new(_assets.Object, _listings.Object, _symbols.Object, [_resolver.Object]);

    [Theory]
    [InlineData(0, 20)]
    [InlineData(1, 51)]
    [InlineData(int.MaxValue, 20)]
    public async Task InvalidPage_IsRejectedBeforeQuery(int page, int pageSize)
    {
        await Assert.ThrowsAsync<ArgumentException>(() => Service().GetAsync(page, pageSize, null, TestContext.Current.CancellationToken));
        _assets.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Get_UsesSevenDayCutoffAndBoundedQuery()
    {
        var before = DateTimeOffset.UtcNow.AddDays(-7);
        _assets.Setup(x => x.GetIncomplete(2, 20, "TEST", It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new IncompleteAssetPage([], 0, 2, 20));
        await Service().GetAsync(2, 20, "TEST", TestContext.Current.CancellationToken);
        _assets.Verify(x => x.GetIncomplete(2, 20, "TEST", It.Is<DateTimeOffset>(d => d >= before && d <= DateTimeOffset.UtcNow.AddDays(-7)), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task NewSymbol_IsValidatedBeforeSavingAndRecordsSuccessfulFetch()
    {
        await Service().ValidateAndSaveSymbolAsync(4, _draft, TestContext.Current.CancellationToken);
        _resolver.Verify(x => x.ValidateAsync(It.Is<MarketDataSymbolResolution>(s => s.Symbol == "TEST" && s.Currency == "PLN" && s.Status == "candidate"), It.IsAny<CancellationToken>()), Times.Once);
        _symbols.Verify(x => x.Add(It.Is<MarketDataSymbol>(s => s.AssetListingId == 4 && s.IsEnabled && s.LastError == null && s.LastSuccessfulPriceFetchAt != null && s.LastValidatedAt == s.LastSuccessfulPriceFetchAt), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExistingDisabledSymbol_IsReenabledWithoutMovingIt()
    {
        var existing = new MarketDataSymbol { Id = 9, AssetListingId = 4, Provider = _draft.Provider, Symbol = "TEST", IsEnabled = false, LastError = "old failure" };
        _symbols.Setup(x => x.Get(_draft.Provider, "TEST", It.IsAny<CancellationToken>())).ReturnsAsync(existing);
        await Service().ValidateAndSaveSymbolAsync(4, _draft, TestContext.Current.CancellationToken);
        Assert.True(existing.IsEnabled);
        Assert.Null(existing.LastError);
        _symbols.Verify(x => x.RecordFetchResult(9, It.Is<DateTimeOffset?>(d => d.HasValue), null, It.IsAny<CancellationToken>()), Times.Once);
        _symbols.Verify(x => x.Add(It.IsAny<MarketDataSymbol>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SymbolOnAnotherListing_IsNeverValidatedOrMoved()
    {
        _symbols.Setup(x => x.Get(_draft.Provider, "TEST", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MarketDataSymbol { AssetListingId = 99 });
        await Assert.ThrowsAsync<ArgumentException>(() => Service().ValidateAndSaveSymbolAsync(4, _draft, TestContext.Current.CancellationToken));
        _resolver.Verify(x => x.ValidateAsync(It.IsAny<MarketDataSymbolResolution>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FailedValidation_DoesNotSaveOrExposeProviderException(bool throws)
    {
        if (throws) _resolver.Setup(x => x.ValidateAsync(It.IsAny<MarketDataSymbolResolution>(), It.IsAny<CancellationToken>())).ThrowsAsync(new HttpRequestException("secret-bearing URL"));
        else _resolver.Setup(x => x.ValidateAsync(It.IsAny<MarketDataSymbolResolution>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);
        var error = await Assert.ThrowsAsync<ArgumentException>(() => Service().ValidateAndSaveSymbolAsync(4, _draft, TestContext.Current.CancellationToken));
        Assert.DoesNotContain("secret-bearing", error.Message);
        _symbols.Verify(x => x.Add(It.IsAny<MarketDataSymbol>(), It.IsAny<CancellationToken>()), Times.Never);
        _symbols.Verify(x => x.Update(It.IsAny<MarketDataSymbol>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}