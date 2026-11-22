using FinanceManager.Application.FinancialAccounts.Investments.Transactions;
using FinanceManager.Domain.Assets.Services;
using FinanceManager.Domain.Dashboard.Services;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.FinancialAccounts.Investments.Entities;
using FinanceManager.Domain.FinancialAccounts.Investments.Repositories;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace FinanceManager.Tests.Unit.Application.FinancialAccounts.Investments.Transactions;

[Trait("Category", "Unit")]
public class InvestmentTransactionPriceRecoveryServiceTests
{
    private const int _userId = 91;
    private static readonly DateOnly _tradeDate = new(2024, 1, 10);

    private readonly Mock<IInvestmentTransactionRepository> _transactionRepository = new();
    private readonly Mock<IInvestmentPriceProvider> _priceProvider = new();
    private readonly Mock<ICacheInvalidator> _cacheInvalidator = new();

    public InvestmentTransactionPriceRecoveryServiceTests()
    {
        _transactionRepository
            .Setup(x => x.Update(It.IsAny<InvestmentTransaction>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
    }

    [Fact]
    public async Task RecoverMissingPricesAsync_GroupsByListingCurrencyAndTradeDate_AndPricesEachGroupOnceInRecordedCurrency()
    {
        var sameGroupFirst = Trade(1, "USD", _tradeDate, 0m);
        var sameGroupSecond = Trade(1, "USD", _tradeDate, -1m);
        var otherCurrency = Trade(1, "EUR", _tradeDate, 0m);
        var otherDate = Trade(1, "USD", _tradeDate.AddDays(1), 0m);
        var otherListing = Trade(2, "USD", _tradeDate, 0m);
        var priced = Trade(1, "USD", _tradeDate, 5m);
        var blankCurrency = Trade(1, " ", _tradeDate, 0m);
        _priceProvider
            .Setup(x => x.GetPricePerUnitAsync(It.IsAny<long>(), It.IsAny<Currency>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((long listingId, Currency currency, DateTime asOf, CancellationToken _) =>
                listingId * 100m + (currency.ShortName == "EUR" ? 10m : 0m) + asOf.Day);

        await CreateService().RecoverMissingPricesAsync(
            [sameGroupFirst, sameGroupSecond, otherCurrency, otherDate, otherListing, priced, blankCurrency],
            _userId,
            TestContext.Current.CancellationToken);

        _priceProvider.Verify(x => x.GetPricePerUnitAsync(It.IsAny<long>(), It.IsAny<Currency>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Exactly(4));
        VerifyPriceRequested(1, "USD", _tradeDate);
        VerifyPriceRequested(1, "EUR", _tradeDate);
        VerifyPriceRequested(1, "USD", _tradeDate.AddDays(1));
        VerifyPriceRequested(2, "USD", _tradeDate);
        Assert.Equal(110m, sameGroupFirst.UnitPrice);
        Assert.Equal(110m, sameGroupSecond.UnitPrice);
        Assert.Equal(120m, otherCurrency.UnitPrice);
        Assert.Equal(111m, otherDate.UnitPrice);
        Assert.Equal(210m, otherListing.UnitPrice);
        Assert.Equal(5m, priced.UnitPrice);
        Assert.Equal(0m, blankCurrency.UnitPrice);
        _transactionRepository.Verify(x => x.Update(It.IsAny<InvestmentTransaction>(), It.IsAny<CancellationToken>()), Times.Exactly(5));
        _transactionRepository.Verify(x => x.Update(priced, It.IsAny<CancellationToken>()), Times.Never);
        _transactionRepository.Verify(x => x.Update(blankCurrency, It.IsAny<CancellationToken>()), Times.Never);
        _cacheInvalidator.Verify(x => x.InvalidateUser(_userId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RecoverMissingPricesAsync_WithoutMissingPrices_DoesNotCallProviderOrInvalidate()
    {
        await CreateService().RecoverMissingPricesAsync(
            [Trade(1, "USD", _tradeDate, 12m)],
            _userId,
            TestContext.Current.CancellationToken);

        _priceProvider.VerifyNoOtherCalls();
        _transactionRepository.Verify(x => x.Update(It.IsAny<InvestmentTransaction>(), It.IsAny<CancellationToken>()), Times.Never);
        _cacheInvalidator.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task RecoverMissingPricesAsync_WhenCallerCancels_RethrowsWithoutInvalidatingEarlierRecoveries()
    {
        using var cancellation = new CancellationTokenSource();
        var recovered = Trade(1, "USD", _tradeDate, 0m);
        var cancelled = Trade(2, "USD", _tradeDate, 0m);
        _priceProvider
            .Setup(x => x.GetPricePerUnitAsync(1, It.IsAny<Currency>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(50m);
        _priceProvider
            .Setup(x => x.GetPricePerUnitAsync(2, It.IsAny<Currency>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .Returns((long _, Currency _, DateTime _, CancellationToken ct) =>
            {
                cancellation.Cancel();
                return Task.FromCanceled<decimal>(ct);
            });

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            CreateService().RecoverMissingPricesAsync([recovered, cancelled], _userId, cancellation.Token));

        Assert.Equal(50m, recovered.UnitPrice);
        Assert.Equal(0m, cancelled.UnitPrice);
        _transactionRepository.Verify(x => x.Update(cancelled, It.IsAny<CancellationToken>()), Times.Never);
        _cacheInvalidator.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task RecoverMissingPricesAsync_WhenProviderTimesOutWithoutCallerCancellation_SkipsOnlyThatGroup()
    {
        var timedOut = Trade(1, "USD", _tradeDate, 0m);
        var recovered = Trade(2, "USD", _tradeDate, 0m);
        _priceProvider
            .Setup(x => x.GetPricePerUnitAsync(1, It.IsAny<Currency>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TaskCanceledException("Provider request timed out."));
        _priceProvider
            .Setup(x => x.GetPricePerUnitAsync(2, It.IsAny<Currency>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(75m);

        await CreateService().RecoverMissingPricesAsync([timedOut, recovered], _userId, TestContext.Current.CancellationToken);

        Assert.Equal(0m, timedOut.UnitPrice);
        Assert.Equal(75m, recovered.UnitPrice);
        _transactionRepository.Verify(x => x.Update(timedOut, It.IsAny<CancellationToken>()), Times.Never);
        _cacheInvalidator.Verify(x => x.InvalidateUser(_userId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RecoverMissingPricesAsync_WhenProviderFails_SkipsOnlyThatGroup()
    {
        var failed = Trade(1, "USD", _tradeDate, 0m);
        var recovered = Trade(2, "USD", _tradeDate, 0m);
        _priceProvider
            .Setup(x => x.GetPricePerUnitAsync(1, It.IsAny<Currency>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Provider unavailable."));
        _priceProvider
            .Setup(x => x.GetPricePerUnitAsync(2, It.IsAny<Currency>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(80m);

        await CreateService().RecoverMissingPricesAsync([failed, recovered], _userId, TestContext.Current.CancellationToken);

        Assert.Equal(0m, failed.UnitPrice);
        Assert.Equal(80m, recovered.UnitPrice);
        _cacheInvalidator.Verify(x => x.InvalidateUser(_userId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public async Task RecoverMissingPricesAsync_WhenProviderReturnsNonPositivePrice_LeavesTradesUnchanged(decimal price)
    {
        var transaction = Trade(1, "USD", _tradeDate, 0m);
        _priceProvider
            .Setup(x => x.GetPricePerUnitAsync(1, It.IsAny<Currency>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(price);

        await CreateService().RecoverMissingPricesAsync([transaction], _userId, TestContext.Current.CancellationToken);

        Assert.Equal(0m, transaction.UnitPrice);
        _transactionRepository.Verify(x => x.Update(It.IsAny<InvestmentTransaction>(), It.IsAny<CancellationToken>()), Times.Never);
        _cacheInvalidator.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task RecoverMissingPricesAsync_WhenUpdateIsRejected_RestoresPreviousPriceAndDoesNotInvalidate()
    {
        var transaction = Trade(1, "USD", _tradeDate, -2m);
        _priceProvider
            .Setup(x => x.GetPricePerUnitAsync(1, It.IsAny<Currency>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(90m);
        decimal? priceSentToRepository = null;
        _transactionRepository
            .Setup(x => x.Update(transaction, It.IsAny<CancellationToken>()))
            .Callback((InvestmentTransaction saved, CancellationToken _) => priceSentToRepository = saved.UnitPrice)
            .ReturnsAsync(false);

        await CreateService().RecoverMissingPricesAsync([transaction], _userId, TestContext.Current.CancellationToken);

        Assert.Equal(90m, priceSentToRepository);
        Assert.Equal(-2m, transaction.UnitPrice);
        _cacheInvalidator.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task RecoverMissingPricesAsync_WhenSomeUpdatesAreRejected_RestoresThemAndInvalidatesOnce()
    {
        var rejected = Trade(1, "USD", _tradeDate, 0m);
        var saved = Trade(1, "USD", _tradeDate, 0m);
        _priceProvider
            .Setup(x => x.GetPricePerUnitAsync(1, It.IsAny<Currency>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(95m);
        _transactionRepository
            .Setup(x => x.Update(rejected, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        await CreateService().RecoverMissingPricesAsync([rejected, saved], _userId, TestContext.Current.CancellationToken);

        Assert.Equal(0m, rejected.UnitPrice);
        Assert.Equal(95m, saved.UnitPrice);
        _priceProvider.Verify(x => x.GetPricePerUnitAsync(It.IsAny<long>(), It.IsAny<Currency>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Once);
        _cacheInvalidator.Verify(x => x.InvalidateUser(_userId, It.IsAny<CancellationToken>()), Times.Once);
    }

    private InvestmentTransactionPriceRecoveryService CreateService() => new(
        _transactionRepository.Object,
        _priceProvider.Object,
        _cacheInvalidator.Object,
        NullLogger<InvestmentTransactionPriceRecoveryService>.Instance);

    private void VerifyPriceRequested(long listingId, string currency, DateOnly tradeDate) =>
        _priceProvider.Verify(
            x => x.GetPricePerUnitAsync(
                listingId,
                It.Is<Currency>(c => c.ShortName == currency),
                tradeDate.ToDateTime(TimeOnly.MinValue),
                It.IsAny<CancellationToken>()),
            Times.Once);

    private static InvestmentTransaction Trade(long listingId, string currency, DateOnly tradeDate, decimal unitPrice) => new()
    {
        UserId = _userId,
        AccountId = 791,
        AssetListingId = listingId,
        Type = InvestmentTransactionType.Buy,
        Quantity = 1m,
        UnitPrice = unitPrice,
        Currency = currency,
        TradeDate = tradeDate
    };
}