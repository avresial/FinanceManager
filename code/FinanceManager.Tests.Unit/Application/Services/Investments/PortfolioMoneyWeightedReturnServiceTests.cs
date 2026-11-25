using FinanceManager.Application.Portfolio.Performance;
using FinanceManager.Application.Transactions.Bonds.Valuation;
using FinanceManager.Domain.Assets.Entities;
using FinanceManager.Domain.Assets.Services;
using FinanceManager.Domain.Currencies.Entities;
using FinanceManager.Domain.Currencies.Services;
using FinanceManager.Domain.Identity.Entities;
using FinanceManager.Domain.MoneyFlow.Entities;
using FinanceManager.Domain.Shared;
using FinanceManager.Domain.Transactions.Bonds.Entities;
using FinanceManager.Domain.Transactions.Bonds.Repositories;
using FinanceManager.Domain.Transactions.Investments.Dtos;
using FinanceManager.Domain.Transactions.Investments.Entities;
using FinanceManager.Domain.Transactions.Investments.Repositories;
using FinanceManager.Domain.Transactions.Shared.Dtos;
using Moq;

namespace FinanceManager.Tests.Unit.Application.Services.Investments;

[Collection("Application")]
[Trait("Category", "Unit")]
public class PortfolioMoneyWeightedReturnServiceTests
{
    private const int _userId = 1;
    private const long _listingId = 100;
    private static readonly DateTime _start = new(2023, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime _end = new(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task GetAsync_UsesEndingPortfolioValueForAnnualizedReturn()
    {
        var transaction = new IInvestmentTransactionRepository.UserCapitalFlowInput(
            1, _listingId,
            DateOnly.FromDateTime(_start),
            InvestmentTransactionType.Buy,
            1m,
            100m,
            null,
            "PLN");
        var fixture = CreateFixture(
            [transaction],
            openingHoldings: new Dictionary<long, decimal>(),
            endingHoldings: new Dictionary<long, decimal> { [_listingId] = 1m });
        fixture.PriceProvider
            .Setup(x => x.GetPricePerUnitAsync(_listingId, DefaultCurrency.PLN, _end, It.IsAny<CancellationToken>()))
            .ReturnsAsync(110m);

        var result = await fixture.Service.GetAsync(_userId, DefaultCurrency.PLN, _start, _end, TestContext.Current.CancellationToken);

        Assert.Equal(MoneyWeightedReturnStatus.Available, result.Status);
        Assert.Equal(0.1m, result.AnnualizedReturn);
    }

    [Fact]
    public async Task GetAsync_IncludesSeparatePurchaseFeeInCashOutflow()
    {
        var transaction = new IInvestmentTransactionRepository.UserCapitalFlowInput(
            1, _listingId, DateOnly.FromDateTime(_start),
            InvestmentTransactionType.Buy, 1m, 100m, 5m, "PLN");
        var fixture = CreateFixture(
            [transaction],
            openingHoldings: new Dictionary<long, decimal>(),
            endingHoldings: new Dictionary<long, decimal> { [_listingId] = 1m });
        fixture.PriceProvider
            .Setup(x => x.GetPricePerUnitAsync(_listingId, DefaultCurrency.PLN, _end, It.IsAny<CancellationToken>()))
            .ReturnsAsync(105m);

        var result = await fixture.Service.GetAsync(_userId, DefaultCurrency.PLN, _start, _end, TestContext.Current.CancellationToken);

        Assert.Equal(MoneyWeightedReturnStatus.Available, result.Status);
        Assert.Equal(0m, result.AnnualizedReturn);
    }

    [Fact]
    public async Task GetAsync_ConvertsCashFlowsUsingHistoricalFx()
    {
        var transaction = new IInvestmentTransactionRepository.UserCapitalFlowInput(
            1, _listingId,
            DateOnly.FromDateTime(_start),
            InvestmentTransactionType.Buy,
            1m,
            100m,
            null,
            "EUR");
        var fixture = CreateFixture(
            [transaction],
            openingHoldings: new Dictionary<long, decimal>(),
            endingHoldings: new Dictionary<long, decimal> { [_listingId] = 1m });
        fixture.PriceProvider
            .Setup(x => x.GetPricePerUnitAsync(_listingId, DefaultCurrency.PLN, _end, It.IsAny<CancellationToken>()))
            .ReturnsAsync(440m);
        fixture.CurrencyExchange
            .Setup(x => x.GetExchangeRateAsync(
                It.Is<Currency>(c => c.ShortName == "EUR"),
                DefaultCurrency.PLN,
                _start,
                _end,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([(_start, 4m), (_end, 4m)]);

        var result = await fixture.Service.GetAsync(_userId, DefaultCurrency.PLN, _start, _end, TestContext.Current.CancellationToken);

        Assert.Equal(MoneyWeightedReturnStatus.Available, result.Status);
        Assert.Equal(0.1m, result.AnnualizedReturn);
        fixture.CurrencyExchange.Verify(x => x.GetExchangeRateAsync(
            It.Is<Currency>(c => c.ShortName == "EUR"),
            DefaultCurrency.PLN,
            _start,
            _end,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetAsync_CarriesHoldingsIntoRangeStartValue()
    {
        var priorTransaction = new IInvestmentTransactionRepository.UserCapitalFlowInput(
            1, _listingId,
            DateOnly.FromDateTime(_start.AddDays(-1)),
            InvestmentTransactionType.Buy,
            1m,
            100m,
            null,
            "PLN");
        var fixture = CreateFixture(
            [priorTransaction],
            openingHoldings: new Dictionary<long, decimal> { [_listingId] = 1m },
            endingHoldings: new Dictionary<long, decimal> { [_listingId] = 1m });
        fixture.PriceProvider
            .Setup(x => x.GetPricePerUnitAsync(_listingId, DefaultCurrency.PLN, _start, It.IsAny<CancellationToken>()))
            .ReturnsAsync(100m);
        fixture.PriceProvider
            .Setup(x => x.GetPricePerUnitAsync(_listingId, DefaultCurrency.PLN, _end, It.IsAny<CancellationToken>()))
            .ReturnsAsync(110m);

        var result = await fixture.Service.GetAsync(_userId, DefaultCurrency.PLN, _start, _end, TestContext.Current.CancellationToken);

        Assert.Equal(MoneyWeightedReturnStatus.Available, result.Status);
        Assert.Equal(0.1m, result.AnnualizedReturn);
    }

    [Fact]
    public async Task GetAsync_WithNoTradesOrBondEntries_ReturnsInsufficientData()
    {
        var fixture = CreateFixture(
            [],
            new Dictionary<long, decimal>(),
            new Dictionary<long, decimal>());

        var result = await fixture.Service.GetAsync(_userId, DefaultCurrency.PLN, _start, _end, TestContext.Current.CancellationToken);

        Assert.Equal(MoneyWeightedReturnStatus.InsufficientData, result.Status);
        Assert.Null(result.AnnualizedReturn);
    }

    [Fact]
    public async Task GetAsync_DoesNotCountRangeStartBondContributionAsOpeningValue()
    {
        var bondRange = new OwnerLedgerRange([], [new OwnerLedgerPoint(5, 1, _start, 1m)]);
        var details = new BondDetails(
            "Bond",
            "Issuer",
            DateOnly.FromDateTime(_start),
            DateOnly.FromDateTime(_end.AddYears(1)),
            [new BondCalculationMethod
            {
                DateOperator = DateOperator.UntilDate,
                DateValue = _end.AddYears(1).ToString("yyyy-MM-dd"),
                Rate = 0m,
            }],
            DefaultCurrency.PLN,
            BondType.InflationBond,
            100m)
        {
            Id = 5,
        };
        var fixture = CreateFixture(
            [],
            new Dictionary<long, decimal>(),
            new Dictionary<long, decimal>(),
            bondRange);
        fixture.BondDetailsRepository
            .Setup(x => x.GetByIdsAsync(It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([details]);

        var result = await fixture.Service.GetAsync(_userId, DefaultCurrency.PLN, _start, _end, TestContext.Current.CancellationToken);

        Assert.Equal(MoneyWeightedReturnStatus.Available, result.Status);
        Assert.Equal(0m, result.AnnualizedReturn);
    }

    [Fact]
    public async Task GetAsync_PoolsBondEntriesFromAllFormerAccountsIntoOnePosition()
    {
        // Entries of one bond (previously two accounts) pool into 3 units worth 300 at the end; the contributions total 300.
        var bondRange = new OwnerLedgerRange([], [new(5, 1, _start.AddDays(10), 1m), new(5, 2, _start.AddDays(20), 2m)]);
        var fixture = CreateFixture(
            [],
            new Dictionary<long, decimal>(),
            new Dictionary<long, decimal>(),
            bondRange);
        SetupZeroRateBond(fixture);

        var result = await fixture.Service.GetAsync(_userId, DefaultCurrency.PLN, _start, _end, TestContext.Current.CancellationToken);

        Assert.Equal(MoneyWeightedReturnStatus.Available, result.Status);
        Assert.InRange(result.AnnualizedReturn!.Value, -0.0001m, 0.0001m);
    }

    [Fact]
    public async Task GetAsync_ReturnsUnavailableWhenAHeldBondHasNoDefinition()
    {
        var fixture = CreateFixture(
            [],
            new Dictionary<long, decimal>(),
            new Dictionary<long, decimal>(),
            new OwnerLedgerRange([], [new(7, 1, _start, 1m)]));

        var result = await fixture.Service.GetAsync(_userId, DefaultCurrency.PLN, _start, _end, TestContext.Current.CancellationToken);

        Assert.Equal(MoneyWeightedReturnStatus.Unavailable, result.Status);
    }

    [Fact]
    public async Task GetAsync_WithUnsetUserId_ReturnsInsufficientDataWithoutReading()
    {
        var fixture = CreateFixture([], new Dictionary<long, decimal>(), new Dictionary<long, decimal>());

        var result = await fixture.Service.GetAsync(0, DefaultCurrency.PLN, _start, _end, TestContext.Current.CancellationToken);

        Assert.Equal(MoneyWeightedReturnStatus.InsufficientData, result.Status);
        fixture.TransactionRepository.Verify(x => x.GetCapitalFlowInputsByUser(It.IsAny<long>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetAsync_UsesTheTradesOfANegativeGuestId()
    {
        const int guestUserId = -4;
        var fixture = CreateFixture(
            [new IInvestmentTransactionRepository.UserCapitalFlowInput(
                1, _listingId, DateOnly.FromDateTime(_start), InvestmentTransactionType.Buy, 1m, 100m, null, "PLN")],
            new Dictionary<long, decimal>(),
            new Dictionary<long, decimal> { [_listingId] = 1m },
            userId: guestUserId);
        fixture.PriceProvider
            .Setup(x => x.GetPricePerUnitAsync(_listingId, DefaultCurrency.PLN, _end, It.IsAny<CancellationToken>()))
            .ReturnsAsync(110m);

        var result = await fixture.Service.GetAsync(guestUserId, DefaultCurrency.PLN, _start, _end, TestContext.Current.CancellationToken);

        Assert.Equal(MoneyWeightedReturnStatus.Available, result.Status);
        Assert.Equal(0.1m, result.AnnualizedReturn);
    }

    // A bond with unit value 100 that accrues nothing, so a pooled position is worth 100 per unit.
    private static void SetupZeroRateBond(Fixture fixture) =>
        fixture.BondDetailsRepository
            .Setup(x => x.GetByIdsAsync(It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new BondDetails(
                "Bond",
                "Issuer",
                DateOnly.FromDateTime(_start),
                DateOnly.FromDateTime(_end.AddYears(1)),
                [new BondCalculationMethod
                {
                    DateOperator = DateOperator.UntilDate,
                    DateValue = _end.AddYears(1).ToString("yyyy-MM-dd"),
                    Rate = 0m,
                }],
                DefaultCurrency.PLN,
                BondType.InflationBond,
                100m)
            { Id = 5 }]);

    private static Fixture CreateFixture(
        IReadOnlyList<IInvestmentTransactionRepository.UserCapitalFlowInput> transactions,
        IReadOnlyDictionary<long, decimal> openingHoldings,
        IReadOnlyDictionary<long, decimal> endingHoldings,
        OwnerLedgerRange? bondRange = null,
        int userId = _userId)
    {
        var bondReader = new Mock<IBondTransactionReader>();
        var transactionRepository = new Mock<IInvestmentTransactionRepository>();
        var priceProvider = new Mock<IInvestmentPriceProvider>();
        var bondDetailsRepository = new Mock<IBondDetailsRepository>();
        var currencyExchange = new Mock<ICurrencyExchangeService>();

        bondReader
            .Setup(x => x.GetValueRange(userId, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(bondRange ?? OwnerLedgerRange.Empty);
        transactionRepository
            .Setup(x => x.GetCapitalFlowInputsByUser(userId, It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(transactions);
        transactionRepository
            .Setup(x => x.GetHoldingsByUserAsOf(userId, It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((long _, DateOnly asOf, CancellationToken _) =>
                ToHoldings(asOf < DateOnly.FromDateTime(_start) ? openingHoldings : endingHoldings));
        bondDetailsRepository
            .Setup(x => x.GetByIdsAsync(It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var service = new PortfolioMoneyWeightedReturnService(
            bondReader.Object,
            transactionRepository.Object,
            priceProvider.Object,
            new BondDashboardContext(bondDetailsRepository.Object),
            currencyExchange.Object);

        return new Fixture(service, priceProvider, currencyExchange, bondDetailsRepository, transactionRepository);
    }

    private static IReadOnlyList<InvestmentHolding> ToHoldings(IReadOnlyDictionary<long, decimal> holdings) =>
        [.. holdings.Select(holding => new InvestmentHolding(holding.Key, "TICK", "Exchange", AssetType.ETF, holding.Value))];

    private sealed record Fixture(
        PortfolioMoneyWeightedReturnService Service,
        Mock<IInvestmentPriceProvider> PriceProvider,
        Mock<ICurrencyExchangeService> CurrencyExchange,
        Mock<IBondDetailsRepository> BondDetailsRepository,
        Mock<IInvestmentTransactionRepository> TransactionRepository);
}