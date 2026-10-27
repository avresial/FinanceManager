using FinanceManager.Application.FinancialAccounts.Investments.Discovery;
using FinanceManager.Domain.Assets.Discovery;
using FinanceManager.Domain.Assets.Entities;
using Moq;

namespace FinanceManager.Tests.Unit.Application.Services.Discovery;

[Trait("Category", "Unit")]
public class MarketDataSymbolResolutionServiceTests
{
    [Fact]
    public async Task ResolveAsync_ValidatesEveryCandidateInPriorityOrderAndIsolatesFailures()
    {
        var first = Resolver(MarketDataProvider.AlphaVantage, 100);
        first.Setup(x => x.ResolveAsync(It.IsAny<InstrumentDiscoveryResultDto>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("secret must not escape"));
        var second = Resolver(MarketDataProvider.TwelveData, 200);
        var third = Resolver(MarketDataProvider.Eodhd, 300);
        var results = await new MarketDataSymbolResolutionService([third.Object, first.Object, second.Object])
            .ResolveAsync(new(), TestContext.Current.CancellationToken);
        Assert.Equal([MarketDataProvider.AlphaVantage, MarketDataProvider.TwelveData, MarketDataProvider.Eodhd], results.Select(x => x.Provider));
        Assert.Equal(["provider unavailable", "resolved", "resolved"], results.Select(x => x.Status));
        Assert.DoesNotContain("secret", results[0].Error);
        second.Verify(x => x.ValidateAsync(It.IsAny<MarketDataSymbolResolution>(), It.IsAny<CancellationToken>()), Times.Once);
        third.Verify(x => x.ValidateAsync(It.IsAny<MarketDataSymbolResolution>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ResolveAsync_NoCandidateSkipsPriceCallAndValidationFailurePreservesCandidateForDiagnostics()
    {
        var first = Resolver(MarketDataProvider.AlphaVantage, 100);
        first.Setup(x => x.ResolveAsync(It.IsAny<InstrumentDiscoveryResultDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MarketDataSymbolResolution(MarketDataProvider.AlphaVantage, 100, "not found"));
        var second = Resolver(MarketDataProvider.TwelveData, 200);
        second.Setup(x => x.ValidateAsync(It.IsAny<MarketDataSymbolResolution>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);
        var results = await new MarketDataSymbolResolutionService([first.Object, second.Object]).ResolveAsync(new(), TestContext.Current.CancellationToken);
        first.Verify(x => x.ValidateAsync(It.IsAny<MarketDataSymbolResolution>(), It.IsAny<CancellationToken>()), Times.Never);
        Assert.Equal("provider unavailable", results[1].Status);
        Assert.NotNull(results[1].Symbol);
        Assert.NotNull(results[1].Error);
    }

    [Fact]
    public async Task ResolveAsync_CallerCancellationStopsTheChain()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var resolver = Resolver(MarketDataProvider.AlphaVantage, 100);
#pragma warning disable xUnit1051
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new MarketDataSymbolResolutionService([resolver.Object]).ResolveAsync(new(), cancellation.Token));
#pragma warning restore xUnit1051
        resolver.Verify(x => x.ResolveAsync(It.IsAny<InstrumentDiscoveryResultDto>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private static Mock<IMarketDataSymbolResolver> Resolver(MarketDataProvider provider, int priority)
    {
        var mock = new Mock<IMarketDataSymbolResolver>();
        mock.SetupGet(x => x.Provider).Returns(provider);
        mock.SetupGet(x => x.Priority).Returns(priority);
        mock.Setup(x => x.ResolveAsync(It.IsAny<InstrumentDiscoveryResultDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MarketDataSymbolResolution(provider, priority, "candidate", "TEST", "LSE", "USD"));
        mock.Setup(x => x.ValidateAsync(It.IsAny<MarketDataSymbolResolution>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        return mock;
    }
}