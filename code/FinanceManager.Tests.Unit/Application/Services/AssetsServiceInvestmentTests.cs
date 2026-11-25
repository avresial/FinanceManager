using FinanceManager.Application.MoneyFlow.Assets;
using FinanceManager.Domain.Assets.Entities;
using FinanceManager.Domain.Assets.Services;
using FinanceManager.Domain.Currencies.Entities;
using FinanceManager.Domain.Currencies.Services;
using FinanceManager.Domain.Portfolio.Dividends.Repositories;
using FinanceManager.Domain.Portfolio.Valuation.Services;
using FinanceManager.Domain.Transactions.Investments.Entities;
using FinanceManager.Domain.Transactions.Investments.Repositories;
using Moq;

namespace FinanceManager.Tests.Unit.Application.Services;

[Collection("Application")]
[Trait("Category", "Unit")]
public class AssetsServiceInvestmentTests
{
    private static AssetsServiceInvestment CreateService(
        Mock<IInvestmentValuationService> valuation, Mock<IInvestmentTransactionRepository>? transactions = null, IDividendPaymentRepository? dividends = null) =>
        new(valuation.Object,
            (transactions ?? new Mock<IInvestmentTransactionRepository>()).Object, Mock.Of<IInvestmentPriceProvider>(),
            Mock.Of<ICurrencyExchangeService>(), dividends ?? Mock.Of<IDividendPaymentRepository>());

    [Fact]
    public async Task IsAnyAccountWithAssets_CountsAnyNonZeroPooledHolding_IncludingGuestUsers()
    {
        var transactions = new Mock<IInvestmentTransactionRepository>();
        transactions.Setup(x => x.GetHoldingsByUserAsOf(4, It.IsAny<DateOnly>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);
        transactions.Setup(x => x.GetHoldingsByUserAsOf(-3, It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new FinanceManager.Domain.Transactions.Investments.Dtos.InvestmentHolding(1, "CSPX", "XLON", AssetType.ETF, -1m)]);
        var service = CreateService(new Mock<IInvestmentValuationService>(), transactions);

        Assert.True(await service.IsAnyAccountWithAssets(-3));
        Assert.False(await service.IsAnyAccountWithAssets(4));
    }

    [Fact]
    public async Task IsAnyAccountWithAssets_IsTrueForAnOwnerWithOnlyRecordedDividends()
    {
        var transactions = new Mock<IInvestmentTransactionRepository>();
        transactions.Setup(x => x.GetHoldingsByUserAsOf(It.IsAny<long>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);
        var dividends = new Mock<IDividendPaymentRepository>();
        dividends.Setup(x => x.HasPayments(-5, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var service = CreateService(new Mock<IInvestmentValuationService>(), transactions, dividends.Object);

        Assert.True(await service.IsAnyAccountWithAssets(-5));
        Assert.False(await service.IsAnyAccountWithAssets(6));
    }

    [Fact]
    public async Task GetAssetsTimeSeries_UsesThePooledOwnerSeries_AndAnswersOnlyForStock()
    {
        var start = new DateTime(2024, 1, 1);
        var end = new DateTime(2024, 1, 3);
        var valuation = new Mock<IInvestmentValuationService>();
        valuation.Setup(x => x.GetUserValueSeriesAsync(1, DefaultCurrency.PLN, start, end, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<DateTime, decimal> { [start] = 10m, [end] = 20m });
        var service = CreateService(valuation);

        var all = await service.GetAssetsTimeSeries(1, DefaultCurrency.PLN, start, end);

        // The valuation leaves out the day that values to zero; the series carries it as an explicit zero.
        Assert.Equal([(start, 10m), (start.AddDays(1), 0m), (end, 20m)], all.Select(x => (x.DateTime, x.Value)));
        Assert.Equal(3, (await service.GetAssetsTimeSeries(1, DefaultCurrency.PLN, start, end, InvestmentType.Stock)).Count);
        Assert.Empty(await service.GetAssetsTimeSeries(1, DefaultCurrency.PLN, start, end, InvestmentType.Bond));
    }

    [Fact]
    public async Task GetAssetsTimeSeries_ReturnsNothing_WhenTheOwnerHoldsNothingInTheRange()
    {
        var start = new DateTime(2024, 1, 1);
        var end = new DateTime(2024, 1, 3);
        var valuation = new Mock<IInvestmentValuationService>();
        valuation.Setup(x => x.GetUserValueSeriesAsync(1, DefaultCurrency.PLN, start, end, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<DateTime, decimal>());

        Assert.Empty(await CreateService(valuation).GetAssetsTimeSeries(1, DefaultCurrency.PLN, start, end));
    }

    [Fact]
    public async Task GetUnrealizedGainLossPerAccount_PreservesRemainingBuyCostAfterListingCurrencyEnrichment()
    {
        var transactionRepository = new Mock<IInvestmentTransactionRepository>();
        var priceProvider = new Mock<IInvestmentPriceProvider>();
        var exchangeService = new Mock<ICurrencyExchangeService>();
        var valuationService = new Mock<IInvestmentValuationService>();
        var tradeDate = new DateOnly(2026, 1, 10);
        var listing = new AssetListing { Id = 11, Ticker = "CSPX" };
        var transactions = new List<InvestmentTransaction>
        {
            new()
            {
                AssetListingId = listing.Id,
                AssetListing = listing,
                Type = InvestmentTransactionType.Buy,
                Quantity = 10m,
                UnitPrice = 100m,
                Fee = 20m,
                Currency = "USD",
                TradeDate = tradeDate,
            },
            new()
            {
                AssetListingId = listing.Id,
                AssetListing = listing,
                Type = InvestmentTransactionType.Sell,
                Quantity = 5m,
                UnitPrice = 120m,
                Currency = "USD",
                TradeDate = tradeDate.AddDays(1),
            },
        };

        transactionRepository.Setup(x => x.GetByUser(1, DateOnly.MinValue, It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(transactions);
        priceProvider.Setup(x => x.GetPricePerUnitAsync(listing.Id, DefaultCurrency.PLN, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(500m);
        exchangeService.Setup(x => x.GetExchangeRateAsync(
                It.Is<Currency>(c => c.ShortName == "USD"), DefaultCurrency.PLN,
                tradeDate.ToDateTime(TimeOnly.MinValue)))
            .ReturnsAsync(4m);

        var service = new AssetsServiceInvestment(
            valuationService.Object,
            transactionRepository.Object,
            priceProvider.Object,
            exchangeService.Object,
            Mock.Of<IDividendPaymentRepository>());

        var result = Assert.Single(await service.GetUnrealizedGainLossPerAccount(1, DefaultCurrency.PLN, DateTime.UtcNow));

        Assert.Equal("Investments", result.AccountName);
        Assert.Equal(2040m, result.CostBasis);
        Assert.Equal(2500m, result.CurrentValue);
        Assert.Equal(460m, result.UnrealizedGainLoss);

        listing.TradingCurrency = "EUR";
        var enrichedResult = Assert.Single(await service.GetUnrealizedGainLossPerAccount(1, DefaultCurrency.PLN, DateTime.UtcNow));

        Assert.Equal(result.CostBasis, enrichedResult.CostBasis);
        Assert.Equal(result.CurrentValue, enrichedResult.CurrentValue);
        Assert.Equal(result.UnrealizedGainLoss, enrichedResult.UnrealizedGainLoss);
        exchangeService.Verify(x => x.GetExchangeRateAsync(
            It.Is<Currency>(c => c.ShortName == "EUR"), It.IsAny<Currency>(), It.IsAny<DateTime>()), Times.Never);
    }

    [Fact]
    public async Task GetUnrealizedGainLossPerInstrument_PoolsTheSameListingAcrossTwoAccountsAndAnUnlinkedTrade_IntoOneAverageCost()
    {
        var transactionRepository = new Mock<IInvestmentTransactionRepository>();
        var priceProvider = new Mock<IInvestmentPriceProvider>();
        var listing = new AssetListing { Id = 11, Ticker = "CSPX" };
        InvestmentTransaction Buy(int? accountId, decimal quantity, decimal unitPrice, int day) => new()
        {
            AssetListingId = listing.Id,
            AssetListing = listing,
            Type = InvestmentTransactionType.Buy,
            Quantity = quantity,
            UnitPrice = unitPrice,
            Currency = "PLN",
            TradeDate = new DateOnly(2026, 1, day),
        };
        // 10 @ 100 in account 7, 10 @ 200 in account 8 and 20 @ 300 unlinked: one pool of 40 units costing 9000 (225 each).
        List<InvestmentTransaction> transactions = [Buy(7, 10m, 100m, 1), Buy(8, 10m, 200m, 2), Buy(null, 20m, 300m, 3)];
        transactions.Add(new()
        {
            AssetListingId = listing.Id,
            AssetListing = listing,
            Type = InvestmentTransactionType.Sell,
            Quantity = 10m,
            UnitPrice = 400m,
            Currency = "PLN",
            TradeDate = new DateOnly(2026, 1, 4),
        });
        transactionRepository.Setup(x => x.GetByUser(-5, DateOnly.MinValue, It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(transactions);
        priceProvider.Setup(x => x.GetPricePerUnitAsync(listing.Id, DefaultCurrency.PLN, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(250m);
        var service = new AssetsServiceInvestment(
            Mock.Of<IInvestmentValuationService>(),
            transactionRepository.Object,
            priceProvider.Object,
            Mock.Of<ICurrencyExchangeService>(), Mock.Of<IDividendPaymentRepository>());

        var instrument = Assert.Single(await service.GetUnrealizedGainLossPerInstrument(-5, DefaultCurrency.PLN, new DateTime(2026, 2, 1)));

        // The 10 units sold leave 30 held at the pooled average of (1000 + 2000 + 6000) / 40 = 225 per unit.
        Assert.Equal(30m, instrument.Quantity);
        Assert.Equal(6750m, instrument.CostBasis);
        Assert.Equal(7500m, instrument.CurrentValue);
        Assert.Equal(750m, instrument.UnrealizedGainLoss);
    }

    [Fact]
    public async Task GetUnrealizedGainLoss_ForAUserWithoutAnId_ReadsNothing()
    {
        var transactionRepository = new Mock<IInvestmentTransactionRepository>();
        var service = new AssetsServiceInvestment(
            Mock.Of<IInvestmentValuationService>(),
            transactionRepository.Object,
            Mock.Of<IInvestmentPriceProvider>(),
            Mock.Of<ICurrencyExchangeService>(), Mock.Of<IDividendPaymentRepository>());

        Assert.Empty(await service.GetUnrealizedGainLossPerInstrument(0, DefaultCurrency.PLN, DateTime.UtcNow));
        Assert.Empty(await service.GetUnrealizedGainLossPerAccount(0, DefaultCurrency.PLN, DateTime.UtcNow));
        transactionRepository.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task GetUnrealizedGainLossPerAccount_FallsBackToAsOfRate_WhenTradeDateRateMissing()
    {
        var transactionRepository = new Mock<IInvestmentTransactionRepository>();
        var priceProvider = new Mock<IInvestmentPriceProvider>();
        var exchangeService = new Mock<ICurrencyExchangeService>();
        var valuationService = new Mock<IInvestmentValuationService>();
        var tradeDate = new DateOnly(2026, 1, 10);
        var asOfDate = new DateTime(2026, 7, 23);
        var listing = new AssetListing { Id = 11, Ticker = "CSPX" };
        var transactions = new List<InvestmentTransaction>
        {
            new()
            {
                AssetListingId = listing.Id,
                AssetListing = listing,
                Type = InvestmentTransactionType.Buy,
                Quantity = 10m,
                UnitPrice = 100m,
                Currency = "USD",
                TradeDate = tradeDate,
            },
        };

        transactionRepository.Setup(x => x.GetByUser(1, DateOnly.MinValue, It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(transactions);
        priceProvider.Setup(x => x.GetPricePerUnitAsync(listing.Id, DefaultCurrency.PLN, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(500m);

        // The trade-date rate is unavailable (returns null); only the as-of-date rate is known.
        exchangeService.Setup(x => x.GetExchangeRateAsync(
                It.Is<Currency>(c => c.ShortName == "USD"), DefaultCurrency.PLN,
                tradeDate.ToDateTime(TimeOnly.MinValue)))
            .ReturnsAsync((decimal?)null);
        exchangeService.Setup(x => x.GetExchangeRateAsync(
                It.Is<Currency>(c => c.ShortName == "USD"), DefaultCurrency.PLN, asOfDate))
            .ReturnsAsync(4m);

        var service = new AssetsServiceInvestment(
            valuationService.Object,
            transactionRepository.Object,
            priceProvider.Object,
            exchangeService.Object,
            Mock.Of<IDividendPaymentRepository>());

        var result = Assert.Single(await service.GetUnrealizedGainLossPerAccount(1, DefaultCurrency.PLN, asOfDate));

        // Capital value falls back to the as-of rate (10 * 100 * 4) instead of collapsing to zero,
        // so gain/loss is a real figure: 5000 current value - 4000 capital value = 1000.
        Assert.Equal(4000m, result.CostBasis);
        Assert.Equal(5000m, result.CurrentValue);
        Assert.Equal(1000m, result.UnrealizedGainLoss);
    }
}