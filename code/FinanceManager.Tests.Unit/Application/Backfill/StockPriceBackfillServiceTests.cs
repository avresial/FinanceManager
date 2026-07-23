using FinanceManager.Application.Assets.Pricing;
using FinanceManager.Application.Backfill;
using FinanceManager.Application.Shared.Options;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace FinanceManager.Tests.Unit.Application.Backfill;

[Trait("Category", "Unit")]
public class StockPriceBackfillServiceTests
{
    private readonly Mock<IInvestmentPriceBackfillService> _backfill = new();
    private readonly BackfillOptions _options = new() { StockPricesEnabled = true, StockLookbackDays = 30 };

    private StockPriceBackfillService CreateSut() => new(
        _backfill.Object,
        Options.Create(_options),
        NullLogger<StockPriceBackfillService>.Instance);

    [Fact]
    public async Task Disabled_DoesNotInvokeWrappedService()
    {
        _options.StockPricesEnabled = false;

        var result = await CreateSut().BackfillAsync(TestContext.Current.CancellationToken);

        Assert.Equal(BackfillResult.Empty, result);
        _backfill.Verify(
            x => x.BackfillAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Enabled_RunsRollingWindowAndMapsResult()
    {
        DateTime? capturedStart = null;
        DateTime? capturedEnd = null;
        _backfill
            .Setup(x => x.BackfillAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .Callback<DateTime, DateTime, CancellationToken>((start, end, _) => (capturedStart, capturedEnd) = (start, end))
            .ReturnsAsync(new InvestmentPriceBackfillResult(Listings: 5, Covered: 4, Uncovered: 1));

        var before = DateTime.UtcNow.Date;
        var result = await CreateSut().BackfillAsync(TestContext.Current.CancellationToken);
        var after = DateTime.UtcNow.Date;

        Assert.Equal(5, result.TargetsInspected);
        Assert.Equal(1, result.Failures);

        // The window is [end - lookback, end] where end is "today" at the moment the SUT ran. Bound
        // the assertion by the clock before/after the call so a UTC-day rollover mid-test can't flake it.
        Assert.NotNull(capturedEnd);
        Assert.InRange(capturedEnd!.Value, before, after);
        Assert.Equal(capturedEnd.Value.AddDays(-30), capturedStart);
    }
}