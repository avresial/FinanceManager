using FinanceManager.Application.Backfill;
using FinanceManager.Application.Backfill.Currencies;
using FinanceManager.Application.Shared.Options;
using FinanceManager.Domain.FinancialAccounts.Currencies.Repositories;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace FinanceManager.Tests.Unit.Application.Backfill;

[Trait("Category", "Unit")]
public class CurrencyRateBackfillServiceTests
{
    private readonly Mock<ICurrencyPairDiscoveryService> _discovery = new();
    private readonly Mock<IFxDailySource> _fxSource = new();
    private readonly Mock<IExchangeRateRepository> _exchangeRates = new();
    private readonly BackfillOptions _options = new() { CurrencyRatesEnabled = true, CurrencyLookbackDays = 30 };

    private CurrencyRateBackfillService CreateSut() => new(
        _discovery.Object,
        _fxSource.Object,
        _exchangeRates.Object,
        Options.Create(_options),
        NullLogger<CurrencyRateBackfillService>.Instance);

    [Fact]
    public async Task Disabled_DoesNothing()
    {
        _options.CurrencyRatesEnabled = false;

        var result = await CreateSut().BackfillAsync(TestContext.Current.CancellationToken);

        Assert.Equal(BackfillResult.Empty, result);
        _discovery.Verify(x => x.GetPairsAsync(It.IsAny<CancellationToken>()), Times.Never);
        _fxSource.Verify(x => x.GetDailyRatesAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task FreshPair_SkipsProviderCall()
    {
        SetupPairs(("USD", "PLN"));
        // A date in the future is always at least the last closed market day, so the pair is fresh
        // regardless of any UTC-day (or Sunday→Monday) rollover between fixture setup and the SUT run.
        _exchangeRates
            .Setup(x => x.GetLatestDate("USD", "PLN", It.IsAny<CancellationToken>()))
            .ReturnsAsync(DateTime.UtcNow.Date.AddDays(1));

        var result = await CreateSut().BackfillAsync(TestContext.Current.CancellationToken);

        _fxSource.Verify(x => x.GetDailyRatesAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        Assert.Equal(1, result.TargetsInspected);
        Assert.Equal(0, result.ProviderRequests);
        Assert.Equal(1, result.RowsSkipped);
        Assert.Equal(0, result.RowsInserted);
    }

    [Fact]
    public async Task StalePair_ImportsOnlyMissingDates()
    {
        var d1 = DateTime.UtcNow.Date.AddDays(-3);
        var d2 = DateTime.UtcNow.Date.AddDays(-2);
        var d3 = DateTime.UtcNow.Date.AddDays(-1);

        SetupPairs(("USD", "PLN"));
        _exchangeRates
            .Setup(x => x.GetLatestDate("USD", "PLN", It.IsAny<CancellationToken>()))
            .ReturnsAsync((DateTime?)null);
        _fxSource
            .Setup(x => x.GetDailyRatesAsync("USD", "PLN", It.IsAny<CancellationToken>()))
            .ReturnsAsync(FxDailyResult.Success(new Dictionary<DateTime, decimal>
            {
                [d1] = 3.9m,
                [d2] = 4.0m,
                [d3] = 4.1m,
            }));
        // d2 is already stored; only d1 and d3 should be inserted.
        _exchangeRates
            .Setup(x => x.GetExistingDates("USD", "PLN", It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([d2]);

        IReadOnlyCollection<(DateTime Date, decimal Rate)>? inserted = null;
        _exchangeRates
            .Setup(x => x.AddRange("USD", "PLN", It.IsAny<IReadOnlyCollection<(DateTime, decimal)>>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, IReadOnlyCollection<(DateTime, decimal)>, CancellationToken>((_, _, rows, _) => inserted = rows)
            .ReturnsAsync((string _, string _, IReadOnlyCollection<(DateTime, decimal)> rows, CancellationToken _) => rows.Count);

        var result = await CreateSut().BackfillAsync(TestContext.Current.CancellationToken);

        Assert.NotNull(inserted);
        Assert.Equal([d1, d3], inserted!.Select(r => r.Date).OrderBy(d => d));
        Assert.Equal(2, result.RowsInserted);
        Assert.Equal(1, result.RowsSkipped);
        Assert.Equal(1, result.ProviderRequests);
        Assert.Equal(1, result.TargetsInspected);
    }

    [Fact]
    public async Task AllReturnedDatesAlreadyStored_InsertsNothing()
    {
        var d1 = DateTime.UtcNow.Date.AddDays(-2);
        var d2 = DateTime.UtcNow.Date.AddDays(-1);

        SetupPairs(("EUR", "PLN"));
        _exchangeRates
            .Setup(x => x.GetLatestDate("EUR", "PLN", It.IsAny<CancellationToken>()))
            .ReturnsAsync((DateTime?)null);
        _fxSource
            .Setup(x => x.GetDailyRatesAsync("EUR", "PLN", It.IsAny<CancellationToken>()))
            .ReturnsAsync(FxDailyResult.Success(new Dictionary<DateTime, decimal> { [d1] = 4.2m, [d2] = 4.3m }));
        _exchangeRates
            .Setup(x => x.GetExistingDates("EUR", "PLN", It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([d1, d2]);

        var result = await CreateSut().BackfillAsync(TestContext.Current.CancellationToken);

        _exchangeRates.Verify(
            x => x.AddRange(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IReadOnlyCollection<(DateTime, decimal)>>(), It.IsAny<CancellationToken>()),
            Times.Never);
        Assert.Equal(0, result.RowsInserted);
        Assert.Equal(2, result.RowsSkipped);
    }

    [Fact]
    public async Task RateLimitResponse_StopsFurtherProviderCalls()
    {
        SetupPairs(("USD", "PLN"), ("EUR", "PLN"));
        _exchangeRates
            .Setup(x => x.GetLatestDate(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((DateTime?)null);
        _fxSource
            .Setup(x => x.GetDailyRatesAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(FxDailyResult.RateLimited);

        var result = await CreateSut().BackfillAsync(TestContext.Current.CancellationToken);

        // Two stale pairs, but the run stops after the first rate-limited response.
        _fxSource.Verify(x => x.GetDailyRatesAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
        _exchangeRates.Verify(
            x => x.AddRange(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IReadOnlyCollection<(DateTime, decimal)>>(), It.IsAny<CancellationToken>()),
            Times.Never);
        Assert.True(result.RateLimited);
    }

    private void SetupPairs(params (string From, string To)[] pairs) =>
        _discovery
            .Setup(x => x.GetPairsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(pairs.Select(p => new CurrencyPair(p.From, p.To)).ToList());
}