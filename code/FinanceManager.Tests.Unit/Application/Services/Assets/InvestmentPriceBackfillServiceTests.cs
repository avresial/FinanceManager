using FinanceManager.Application.Assets.Pricing;
using FinanceManager.Domain.Assets.Services;
using FinanceManager.Domain.FinancialAccounts.Investments.Repositories;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace FinanceManager.Tests.Unit.Application.Services.Assets;

[Trait("Category", "Unit")]
public class InvestmentPriceBackfillServiceTests
{
    private static readonly DateTime _start = new(2026, 7, 11);
    private static readonly DateTime _end = new(2026, 7, 18);

    private readonly Mock<IInvestmentTransactionRepository> _transactionRepository = new();
    private readonly Mock<IInvestmentPriceProvider> _priceProvider = new();

    private InvestmentPriceBackfillService CreateSut() => new(
        _transactionRepository.Object,
        _priceProvider.Object,
        NullLogger<InvestmentPriceBackfillService>.Instance);

    [Fact]
    public async Task Backfill_EnsuresQuotesForEveryDistinctListing()
    {
        _transactionRepository
            .Setup(x => x.GetDistinctAssetListingIds(It.IsAny<CancellationToken>()))
            .ReturnsAsync([10L, 20L, 30L]);
        _priceProvider
            .Setup(x => x.EnsureQuotesAsync(It.IsAny<long>(), _start, _end, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await CreateSut().BackfillAsync(_start, _end, TestContext.Current.CancellationToken);

        Assert.Equal(new InvestmentPriceBackfillResult(3, 3, 0), result);
        _priceProvider.Verify(x => x.EnsureQuotesAsync(10L, _start, _end, It.IsAny<CancellationToken>()), Times.Once);
        _priceProvider.Verify(x => x.EnsureQuotesAsync(20L, _start, _end, It.IsAny<CancellationToken>()), Times.Once);
        _priceProvider.Verify(x => x.EnsureQuotesAsync(30L, _start, _end, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Backfill_CountsListingsWithoutQuotesAsUncovered()
    {
        _transactionRepository
            .Setup(x => x.GetDistinctAssetListingIds(It.IsAny<CancellationToken>()))
            .ReturnsAsync([10L, 20L]);
        _priceProvider
            .Setup(x => x.EnsureQuotesAsync(10L, _start, _end, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _priceProvider
            .Setup(x => x.EnsureQuotesAsync(20L, _start, _end, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await CreateSut().BackfillAsync(_start, _end, TestContext.Current.CancellationToken);

        Assert.Equal(new InvestmentPriceBackfillResult(2, 1, 1), result);
    }

    [Fact]
    public async Task Backfill_WithNoHeldListings_DoesNotTouchPriceProvider()
    {
        _transactionRepository
            .Setup(x => x.GetDistinctAssetListingIds(It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var result = await CreateSut().BackfillAsync(_start, _end, TestContext.Current.CancellationToken);

        Assert.Equal(new InvestmentPriceBackfillResult(0, 0, 0), result);
        _priceProvider.Verify(
            x => x.EnsureQuotesAsync(It.IsAny<long>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}