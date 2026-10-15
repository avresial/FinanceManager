using FinanceManager.Application.FinancialAccounts.Bond.Valuation;
using FinanceManager.Application.FinancialAccounts.Investments.Performance;
using FinanceManager.Domain.Assets.Services;
using FinanceManager.Domain.FinancialAccounts.Bond.Entities;
using FinanceManager.Domain.FinancialAccounts.Bond.Repositories;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.FinancialAccounts.Currencies.Services;
using FinanceManager.Domain.FinancialAccounts.Investments.Entities;
using FinanceManager.Domain.FinancialAccounts.Investments.Repositories;
using FinanceManager.Domain.FinancialAccounts.Shared.Repositories;
using FinanceManager.Domain.Identity.Entities;
using FinanceManager.Domain.MoneyFlow.Entities;
using FinanceManager.Domain.Shared;
using Moq;

namespace FinanceManager.Tests.Unit.Application.Services.Investments;

[Collection("Application")]
[Trait("Category", "Unit")]
public class PortfolioReturnConsistencyTests
{
    private const int _userId = 1;
    private const int _accountId = 10;
    private const long _listingId = 100;
    private static readonly DateTime _start = new(2023, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime _end = new(2023, 1, 5, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task GetAsync_AllReturnServicesAgreeAcrossLossContributionGainAndWithdrawal()
    {
        var transactions = new[]
        {
            Flow(0, _start.AddDays(-1), InvestmentTransactionType.Buy, 10m, 90m),
            Flow(1, _start.AddDays(2), InvestmentTransactionType.Buy, 20m, 50m),
            Flow(2, _end, InvestmentTransactionType.Sell, 10m, 60m),
        };
        var fixture = CreateFixture(
            transactions,
            openingQuantity: 10m,
            endingQuantity: 20m,
            prices: [(0, 100m), (1, 50m), (2, 50m), (3, 60m), (4, 60m)]);

        var timeWeighted = await fixture.TimeWeighted.GetAsync(
            _userId, DefaultCurrency.PLN, _start, _end, TestContext.Current.CancellationToken);
        var moneyWeighted = await fixture.MoneyWeighted.GetAsync(
            _userId, DefaultCurrency.PLN, _start, _end, TestContext.Current.CancellationToken);
        var attribution = await fixture.Attribution.GetAsync(
            _userId, DefaultCurrency.PLN, _start, _end, TestContext.Current.CancellationToken);

        Assert.Equal(TimeWeightedReturnStatus.Available, timeWeighted.Status);
        Assert.Equal(-0.4m, timeWeighted.TotalReturn);
        Assert.Equal(MoneyWeightedReturnStatus.Available, moneyWeighted.Status);
        Assert.NotNull(moneyWeighted.AnnualizedReturn);
        Assert.InRange(
            Math.Abs((decimal)EvaluateNpv(
                [(_start, -1_000m), (_start.AddDays(2), -1_000m), (_end, 1_800m)],
                _start,
                (double)moneyWeighted.AnnualizedReturn.Value)),
            0m,
            0.01m);

        Assert.Equal(PortfolioReturnAttributionStatus.Available, attribution.Status);
        Assert.Equal(200m, attribution.TotalChange);
        Assert.Equal(400m, attribution.ExternalCashMovement);
        Assert.Equal(-200m, attribution.MarketEffect);
        Assert.Equal(0m, attribution.FeeEffect);
        Assert.Equal(0m, attribution.FxEffect);
        Assert.Equal(0m, attribution.ReconciliationDifference);
        Assert.Equal(
            attribution.TotalChange,
            attribution.ExternalCashMovement
            + attribution.MarketEffect
            + attribution.FeeEffect
            + attribution.FxEffect
            + attribution.ReconciliationDifference);

        fixture.TransactionRepository.Verify(repository => repository.GetValuationInputs(
            It.Is<IReadOnlyCollection<int>>(ids => ids.SequenceEqual(new[] { _accountId })),
            DateOnly.FromDateTime(_start),
            DateOnly.FromDateTime(_end),
            It.IsAny<CancellationToken>()), Times.Once);
        fixture.TransactionRepository.Verify(repository => repository.GetHoldingsByAccountAsOf(
            It.IsAny<IReadOnlyCollection<int>>(),
            DateOnly.FromDateTime(_start).AddDays(-1),
            It.IsAny<CancellationToken>()), Times.Exactly(2));
        fixture.TransactionRepository.Verify(repository => repository.GetHoldingsByAccountAsOf(
            It.IsAny<IReadOnlyCollection<int>>(),
            DateOnly.FromDateTime(_end),
            It.IsAny<CancellationToken>()), Times.Exactly(2));
        fixture.TransactionRepository.Verify(repository => repository.GetCapitalFlowInputs(
            It.IsAny<IReadOnlyCollection<int>>(),
            DateOnly.FromDateTime(_end),
            It.IsAny<CancellationToken>()), Times.Exactly(3));
    }

    [Fact]
    public async Task GetAsync_LargeLateContributionCanMakeXirrPositiveWhileTwrIsNegative()
    {
        var end = _start.AddDays(30);
        const double expectedXirr = 2.0318d;
        const decimal endingPrice = 96.18m;
        var discountFactor = Math.Pow(1d + expectedXirr, 15d / 365d);
        var contribution = (decimal)((1_000d * discountFactor * discountFactor - 961.8d)
                                     / (1.20225d - discountFactor));
        var contributionQuantity = contribution / 80m;
        var fixture = CreateFixture(
            [Flow(1, _start.AddDays(15), InvestmentTransactionType.Buy, contributionQuantity, 80m)],
            openingQuantity: 10m,
            endingQuantity: 10m + contributionQuantity,
            prices: [(0, 100m), (1, 80m), (15, 80m), (30, endingPrice)],
            end: end);

        var timeWeighted = await fixture.TimeWeighted.GetAsync(
            _userId, DefaultCurrency.PLN, _start, end, TestContext.Current.CancellationToken);
        var moneyWeighted = await fixture.MoneyWeighted.GetAsync(
            _userId, DefaultCurrency.PLN, _start, end, TestContext.Current.CancellationToken);
        var attribution = await fixture.Attribution.GetAsync(
            _userId, DefaultCurrency.PLN, _start, end, TestContext.Current.CancellationToken);
        var endingValue = (10m + contributionQuantity) * endingPrice;
        var cashFlows = new[]
        {
            (_start, -1_000m),
            (_start.AddDays(15), -contribution),
            (end, endingValue),
        };

        Assert.Equal(TimeWeightedReturnStatus.Available, timeWeighted.Status);
        Assert.Equal(-0.0382m, timeWeighted.TotalReturn);
        Assert.Equal(MoneyWeightedReturnStatus.Available, moneyWeighted.Status);
        Assert.NotNull(moneyWeighted.AnnualizedReturn);
        Assert.Equal((decimal)expectedXirr, moneyWeighted.AnnualizedReturn!.Value, precision: 6);
        Assert.True(moneyWeighted.AnnualizedReturn > 0m);
        Assert.InRange(
            Math.Abs((decimal)EvaluateNpv(cashFlows, _start, (double)moneyWeighted.AnnualizedReturn.Value)),
            0m,
            0.01m);

        Assert.Equal(PortfolioReturnAttributionStatus.Available, attribution.Status);
        Assert.Equal(endingValue - 1_000m, attribution.TotalChange);
        Assert.Equal(contribution, attribution.ExternalCashMovement);
        Assert.Equal(attribution.TotalChange - contribution, attribution.MarketEffect);
        Assert.Equal(
            attribution.TotalChange,
            attribution.ExternalCashMovement
            + attribution.MarketEffect
            + attribution.FeeEffect
            + attribution.FxEffect
            + attribution.ReconciliationDifference);
    }

    [Theory]
    [InlineData(30)]
    [InlineData(365)]
    public async Task GetAsync_NoFlowsAnnualizesElapsedDaysRatherThanInclusiveDateCount(int elapsedDays)
    {
        var end = _start.AddDays(elapsedDays);
        var fixture = CreateFixture(
            [],
            openingQuantity: 10m,
            endingQuantity: 10m,
            prices: [(0, 100m), (elapsedDays, 110m)],
            end: end);

        var timeWeighted = await fixture.TimeWeighted.GetAsync(
            _userId, DefaultCurrency.PLN, _start, end, TestContext.Current.CancellationToken);
        var moneyWeighted = await fixture.MoneyWeighted.GetAsync(
            _userId, DefaultCurrency.PLN, _start, end, TestContext.Current.CancellationToken);

        Assert.Equal(TimeWeightedReturnStatus.Available, timeWeighted.Status);
        Assert.Equal(MoneyWeightedReturnStatus.Available, moneyWeighted.Status);
        Assert.Equal(0.1m, timeWeighted.TotalReturn);
        Assert.Equal((decimal)(Math.Pow(1.1d, 365d / elapsedDays) - 1d), moneyWeighted.AnnualizedReturn!.Value, precision: 8);
        Assert.InRange(
            Math.Abs((decimal)EvaluateNpv(
                [(_start, -1_000m), (end, 1_100m)],
                _start,
                (double)moneyWeighted.AnnualizedReturn!.Value)),
            0m,
            0.0001m);
    }

    [Fact]
    public async Task GetAsync_IncludesOpeningDayBuyAndClosingDayWithdrawalInAllThreeServices()
    {
        var end = _start.AddDays(30);
        var fixture = CreateFixture(
            [Flow(1, _start, InvestmentTransactionType.Buy, 5m, 100m),
             Flow(2, end, InvestmentTransactionType.Sell, 5m, 110m)],
            openingQuantity: 10m,
            endingQuantity: 10m,
            prices: [(0, 100m), (29, 110m), (30, 110m)],
            end: end);

        var timeWeighted = await fixture.TimeWeighted.GetAsync(
            _userId, DefaultCurrency.PLN, _start, end, TestContext.Current.CancellationToken);
        var moneyWeighted = await fixture.MoneyWeighted.GetAsync(
            _userId, DefaultCurrency.PLN, _start, end, TestContext.Current.CancellationToken);
        var attribution = await fixture.Attribution.GetAsync(
            _userId, DefaultCurrency.PLN, _start, end, TestContext.Current.CancellationToken);

        Assert.Equal(0.1m, timeWeighted.TotalReturn);
        Assert.Equal((decimal)(Math.Pow(1.1d, 365d / 30d) - 1d), moneyWeighted.AnnualizedReturn!.Value, precision: 8);
        Assert.Equal(100m, attribution.TotalChange);
        Assert.Equal(-50m, attribution.ExternalCashMovement);
        Assert.Equal(150m, attribution.MarketEffect);
        Assert.Equal(0m, attribution.ReconciliationDifference);
    }

    private static IInvestmentTransactionRepository.CapitalFlowInput Flow(
        long id,
        DateTime date,
        InvestmentTransactionType type,
        decimal quantity,
        decimal unitPrice) =>
        new(id, _accountId, _listingId, DateOnly.FromDateTime(date), type, quantity, unitPrice, null, "PLN", null);

    private static Fixture CreateFixture(
        IReadOnlyList<IInvestmentTransactionRepository.CapitalFlowInput> transactions,
        decimal openingQuantity,
        decimal endingQuantity,
        (int Day, decimal Value)[] prices,
        DateTime? end = null)
    {
        var rangeEnd = end ?? _end;
        var accountRepository = new Mock<IFinancialAccountRepository>();
        var transactionRepository = new Mock<IInvestmentTransactionRepository>();
        var priceProvider = new Mock<IInvestmentPriceProvider>();
        var bondDetailsRepository = new Mock<IBondDetailsRepository>();
        var currencyExchange = new Mock<ICurrencyExchangeService>();
        var investmentAccount = new InvestmentAccount(_userId, _accountId, "Investments");
        var orderedPrices = prices.OrderBy(price => price.Day).ToArray();
        var priceSeries = Enumerable
            .Range(0, (rangeEnd.Date - _start.Date).Days + 1)
            .ToDictionary(
                offset => _start.AddDays(offset),
                offset => orderedPrices.Last(price => price.Day <= offset).Value);

        accountRepository
            .Setup(repository => repository.GetAccounts<InvestmentAccount>(
                _userId, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<bool>()))
            .Returns(new[] { investmentAccount }.ToAsyncEnumerable());
        accountRepository
            .Setup(repository => repository.GetAccounts<BondAccount>(
                _userId, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<bool>()))
            .Returns(Array.Empty<BondAccount>().ToAsyncEnumerable());
        transactionRepository
            .Setup(repository => repository.GetCapitalFlowInputs(
                It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(transactions);
        transactionRepository
            .Setup(repository => repository.GetValuationInputs(
                It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new IInvestmentTransactionRepository.AccountValuationInputs(
                [new(_accountId, _listingId, openingQuantity)],
                transactions
                    .Where(transaction => transaction.TradeDate >= DateOnly.FromDateTime(_start)
                                          && transaction.TradeDate <= DateOnly.FromDateTime(rangeEnd))
                    .Select(transaction => new IInvestmentTransactionRepository.ValuationTradeInput(
                        transaction.AccountId,
                        transaction.AssetListingId,
                        transaction.TradeDate,
                        transaction.Type == InvestmentTransactionType.Buy ? transaction.Quantity : -transaction.Quantity))
                    .ToList()));
        transactionRepository
            .Setup(repository => repository.GetHoldingsByAccountAsOf(
                It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyCollection<int> _, DateOnly asOf, CancellationToken _) =>
                asOf < DateOnly.FromDateTime(_start)
                    ? Holdings(openingQuantity)
                    : Holdings(endingQuantity));
        bondDetailsRepository
            .Setup(repository => repository.GetByIdsAsync(
                It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        currencyExchange
            .Setup(service => service.GetExchangeRateAsync(
                It.IsAny<Currency>(),
                It.IsAny<Currency>(),
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        priceProvider
            .Setup(provider => provider.GetPricePerUnitSeriesAsync(
                _listingId,
                DefaultCurrency.PLN,
                _start,
                rangeEnd,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyDictionary<DateTime, decimal>)priceSeries);
        priceProvider
            .Setup(provider => provider.GetPricePerUnitAsync(
                _listingId,
                DefaultCurrency.PLN,
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((long _, Currency _, DateTime date, CancellationToken _) => priceSeries[date.Date]);

        return new Fixture(
            new PortfolioTimeWeightedReturnService(
                accountRepository.Object,
                transactionRepository.Object,
                priceProvider.Object,
                new BondDashboardContext(bondDetailsRepository.Object),
                currencyExchange.Object),
            new PortfolioMoneyWeightedReturnService(
                accountRepository.Object,
                transactionRepository.Object,
                priceProvider.Object,
                new BondDashboardContext(bondDetailsRepository.Object),
                currencyExchange.Object),
            new PortfolioReturnAttributionService(
                accountRepository.Object,
                transactionRepository.Object,
                priceProvider.Object,
                new BondDashboardContext(bondDetailsRepository.Object),
                currencyExchange.Object),
            transactionRepository);
    }

    private static Dictionary<int, IReadOnlyDictionary<long, decimal>> Holdings(decimal quantity) =>
        new()
        {
            [_accountId] = new Dictionary<long, decimal> { [_listingId] = quantity },
        };

    private static double EvaluateNpv(
        IReadOnlyList<(DateTime Date, decimal Amount)> flows,
        DateTime baseDate,
        double annualizedRate) =>
        flows.Sum(flow => (double)flow.Amount
                          / Math.Pow(1d + annualizedRate, (flow.Date - baseDate).TotalDays / 365d));

    private sealed record Fixture(
        PortfolioTimeWeightedReturnService TimeWeighted,
        PortfolioMoneyWeightedReturnService MoneyWeighted,
        PortfolioReturnAttributionService Attribution,
        Mock<IInvestmentTransactionRepository> TransactionRepository);
}