using FinanceManager.Application.FinancialAccounts.Stock.Resolution;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.FinancialAccounts.Currencies.Services;
using FinanceManager.Domain.FinancialAccounts.Shared.Services;
using FinanceManager.Domain.Identity.Services;
using Microsoft.Extensions.Logging;
using Moq;

namespace FinanceManager.Tests.Unit.Application.Services.Stocks;

[Collection("Application")]
[Trait("Category", "Unit")]
public class QuoteFactorResolverTests
{
    private readonly Mock<ICurrencyExchangeService> _exchangeRateProviderMock = new();
    private readonly ILogger<QuoteFactorResolver> _logger = LoggerFactory.Create(_ => { }).CreateLogger<QuoteFactorResolver>();

    private QuoteFactorResolver CreateResolver() => new(_exchangeRateProviderMock.Object, _logger);

    [Fact]
    public async Task ResolveAsync_GbxToGbp_Returns0point01()
    {
        var resolver = CreateResolver();

        var factor = await resolver.ResolveAsync("GBX", "GBP", TestContext.Current.CancellationToken);

        // 1 GBX (penny) = 0.01 GBP; contract is multiply from-amount by factor to get to-amount.
        Assert.Equal(0.01m, factor);
        _exchangeRateProviderMock.Verify(
            x => x.GetExchangeRateAsync(It.IsAny<Currency>(), It.IsAny<Currency>(), It.IsAny<DateTime>()),
            Times.Never);
    }

    [Fact]
    public async Task ResolveAsync_GbpToGbx_Returns100()
    {
        var resolver = CreateResolver();

        var factor = await resolver.ResolveAsync("GBP", "GBX", TestContext.Current.CancellationToken);

        Assert.Equal(100m, factor);
    }

    [Fact]
    public async Task ResolveAsync_SameCurrency_Returns1()
    {
        var resolver = CreateResolver();

        var factor = await resolver.ResolveAsync("USD", "USD", TestContext.Current.CancellationToken);

        Assert.Equal(1m, factor);
    }

    [Fact]
    public async Task ResolveAsync_UnknownPair_FallsBackToExchangeRateApi()
    {
        var resolver = CreateResolver();
        _exchangeRateProviderMock
            .Setup(x => x.GetExchangeRateAsync(
                It.Is<Currency>(c => c.ShortName == "USD"),
                It.Is<Currency>(c => c.ShortName == "EUR"),
                It.IsAny<DateTime>()))
            .ReturnsAsync(0.92m);

        var factor = await resolver.ResolveAsync("USD", "EUR", TestContext.Current.CancellationToken);

        Assert.Equal(0.92m, factor);
    }

    [Fact]
    public async Task ResolveAsync_WhenExchangeApiFails_Returns1()
    {
        var resolver = CreateResolver();
        _exchangeRateProviderMock
            .Setup(x => x.GetExchangeRateAsync(It.IsAny<Currency>(), It.IsAny<Currency>(), It.IsAny<DateTime>()))
            .ThrowsAsync(new HttpRequestException("network down"));

        var factor = await resolver.ResolveAsync("USD", "JPY", TestContext.Current.CancellationToken);

        Assert.Equal(1m, factor);
    }
}