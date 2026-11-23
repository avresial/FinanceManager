using FinanceManager.Application.FinancialAccounts.Bond.Valuation;
using FinanceManager.Application.FinancialAccounts.Investments.Performance;
using FinanceManager.Domain.Assets.Entities;
using FinanceManager.Domain.Assets.Services;
using FinanceManager.Domain.FinancialAccounts.Bond.Entities;
using FinanceManager.Domain.FinancialAccounts.Bond.Repositories;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.FinancialAccounts.Currencies.Services;
using FinanceManager.Domain.FinancialAccounts.Investments.Dtos;
using FinanceManager.Domain.FinancialAccounts.Investments.Entities;
using FinanceManager.Domain.FinancialAccounts.Investments.Repositories;
using FinanceManager.Domain.Identity.Entities;
using FinanceManager.Domain.MoneyFlow.Entities;
using FinanceManager.Domain.Shared;
using FinanceManager.Domain.Transactions.Bonds.Repositories;
using FinanceManager.Domain.Transactions.Shared.Dtos;
using Moq;

namespace FinanceManager.Tests.Unit.Application.Services.Investments;

[Collection("Application")]
[Trait("Category", "Unit")]
public class PortfolioReturnAttributionServiceTests
{
    private const int _userId = 1;
    private const long _listingId = 100;
    private static readonly DateTime _start = new(2023, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime _end = new(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task GetAsync_SeparatesContributionFeesAndMarketEffect()
    {
        var transaction = Flow(
            1,
            _start,
            InvestmentTransactionType.Buy,
            quantity: 1m,
            unitPrice: 100m,
            fee: 5m,
            currency: "PLN");
        var fixture = CreateFixture(
            [transaction],
            openingHoldings: new Dictionary<long, decimal>(),
            endingHoldings: Holdings(1m));
        fixture.PriceProvider
            .Setup(x => x.GetPricePerUnitAsync(_listingId, DefaultCurrency.PLN, _end, It.IsAny<CancellationToken>()))
            .ReturnsAsync(120m);

        var result = await fixture.Service.GetAsync(_userId, DefaultCurrency.PLN, _start, _end, TestContext.Current.CancellationToken);

        Assert.Equal(PortfolioReturnAttributionStatus.Available, result.Status);
        Assert.Equal(120m, result.TotalChange);
        Assert.Equal(100m, result.ExternalCashMovement);
        Assert.Equal(-5m, result.FeeEffect);
        Assert.Equal(25m, result.MarketEffect);
        Assert.Equal(0m, result.FxEffect);
        Assert.Equal(0m, result.ReconciliationDifference);
        Assert.Null(result.DividendIncome);
        Assert.Contains(result.UnsupportedComponents, component => component.Name == "Dividends / income");
    }

    [Fact]
    public async Task GetAsync_SeparatesWithdrawalFromRangeStartValue()
    {
        var priorBuy = Flow(1, _start.AddDays(-1), InvestmentTransactionType.Buy, 1m, 100m, currency: "PLN");
        var sale = Flow(2, _end, InvestmentTransactionType.Sell, 1m, 90m, currency: "PLN");
        var fixture = CreateFixture(
            [priorBuy, sale],
            openingHoldings: Holdings(1m),
            endingHoldings: new Dictionary<long, decimal>());
        fixture.PriceProvider
            .Setup(x => x.GetPricePerUnitAsync(_listingId, DefaultCurrency.PLN, _start, It.IsAny<CancellationToken>()))
            .ReturnsAsync(100m);

        var result = await fixture.Service.GetAsync(_userId, DefaultCurrency.PLN, _start, _end, TestContext.Current.CancellationToken);

        Assert.Equal(PortfolioReturnAttributionStatus.Available, result.Status);
        Assert.Equal(-100m, result.TotalChange);
        Assert.Equal(-90m, result.ExternalCashMovement);
        Assert.Equal(-10m, result.MarketEffect);
        Assert.Equal(0m, result.FeeEffect);
        Assert.Equal(0m, result.ReconciliationDifference);
    }

    [Fact]
    public async Task GetAsync_AttributesHistoricalForeignExchangeEffect()
    {
        var transaction = Flow(1, _start, InvestmentTransactionType.Buy, 1m, 100m, currency: "EUR");
        var fixture = CreateFixture(
            [transaction],
            openingHoldings: new Dictionary<long, decimal>(),
            endingHoldings: Holdings(1m));
        fixture.PriceProvider
            .Setup(x => x.GetPricePerUnitAsync(_listingId, DefaultCurrency.PLN, _end, It.IsAny<CancellationToken>()))
            .ReturnsAsync(450m);
        fixture.CurrencyExchange
            .Setup(x => x.GetExchangeRateAsync(
                It.Is<Currency>(currency => currency.ShortName == "EUR"),
                DefaultCurrency.PLN,
                _start,
                _end,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([(_start, 4m), (_end, 4.5m)]);

        var result = await fixture.Service.GetAsync(_userId, DefaultCurrency.PLN, _start, _end, TestContext.Current.CancellationToken);

        Assert.Equal(PortfolioReturnAttributionStatus.Available, result.Status);
        Assert.Equal(450m, result.TotalChange);
        Assert.Equal(400m, result.ExternalCashMovement);
        Assert.Equal(50m, result.FxEffect);
        Assert.Equal(0m, result.MarketEffect);
        Assert.Equal(0m, result.ReconciliationDifference);
    }

    [Fact]
    public async Task GetAsync_DoesNotAttributeFxBeforeLateContribution()
    {
        var transaction = Flow(1, _end, InvestmentTransactionType.Buy, 1m, 100m, currency: "EUR");
        var fixture = CreateFixture(
            [transaction],
            openingHoldings: new Dictionary<long, decimal>(),
            endingHoldings: Holdings(1m));
        fixture.PriceProvider
            .Setup(x => x.GetPricePerUnitAsync(_listingId, DefaultCurrency.PLN, _end, It.IsAny<CancellationToken>()))
            .ReturnsAsync(450m);
        fixture.CurrencyExchange
            .Setup(x => x.GetExchangeRateAsync(
                It.Is<Currency>(currency => currency.ShortName == "EUR"),
                DefaultCurrency.PLN,
                _start,
                _end,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([(_start, 4m), (_end, 4.5m)]);

        var result = await fixture.Service.GetAsync(_userId, DefaultCurrency.PLN, _start, _end, TestContext.Current.CancellationToken);

        Assert.Equal(PortfolioReturnAttributionStatus.Available, result.Status);
        Assert.Equal(450m, result.TotalChange);
        Assert.Equal(450m, result.ExternalCashMovement);
        Assert.Equal(0m, result.FxEffect);
        Assert.Equal(0m, result.MarketEffect);
        Assert.Equal(0m, result.ReconciliationDifference);
    }

    [Fact]
    public async Task GetAsync_TracksZeroPricePurchaseForFxAttribution()
    {
        var transaction = Flow(1, _start, InvestmentTransactionType.Buy, 1m, 0m, currency: "EUR");
        var fixture = CreateFixture(
            [transaction],
            openingHoldings: new Dictionary<long, decimal>(),
            endingHoldings: Holdings(1m));
        fixture.PriceProvider
            .Setup(x => x.GetPricePerUnitAsync(_listingId, DefaultCurrency.PLN, _end, It.IsAny<CancellationToken>()))
            .ReturnsAsync(450m);
        fixture.CurrencyExchange
            .Setup(x => x.GetExchangeRateAsync(
                It.Is<Currency>(currency => currency.ShortName == "EUR"),
                DefaultCurrency.PLN,
                _start,
                _end,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([(_start, 4m), (_end, 4.5m)]);

        var result = await fixture.Service.GetAsync(_userId, DefaultCurrency.PLN, _start, _end, TestContext.Current.CancellationToken);

        Assert.Equal(PortfolioReturnAttributionStatus.Available, result.Status);
        Assert.Equal(0m, result.ExternalCashMovement);
        Assert.Equal(50m, result.FxEffect);
        Assert.Equal(0m, result.ReconciliationDifference);
    }

    [Fact]
    public async Task GetAsync_MissingHistoricalExchangeRateReturnsUnavailable()
    {
        var transaction = Flow(1, _start, InvestmentTransactionType.Buy, 1m, 100m, currency: "EUR");
        var fixture = CreateFixture(
            [transaction],
            openingHoldings: new Dictionary<long, decimal>(),
            endingHoldings: Holdings(1m));

        var result = await fixture.Service.GetAsync(_userId, DefaultCurrency.PLN, _start, _end, TestContext.Current.CancellationToken);

        Assert.Equal(PortfolioReturnAttributionStatus.Unavailable, result.Status);
        Assert.Null(result.TotalChange);
    }

    [Fact]
    public async Task GetAsync_CarriesHoldingsIntoRangeStart()
    {
        var priorBuy = Flow(1, _start.AddDays(-1), InvestmentTransactionType.Buy, 1m, 100m, currency: "PLN");
        var fixture = CreateFixture(
            [priorBuy],
            openingHoldings: Holdings(1m),
            endingHoldings: Holdings(1m));
        fixture.PriceProvider
            .Setup(x => x.GetPricePerUnitAsync(_listingId, DefaultCurrency.PLN, _start, It.IsAny<CancellationToken>()))
            .ReturnsAsync(100m);
        fixture.PriceProvider
            .Setup(x => x.GetPricePerUnitAsync(_listingId, DefaultCurrency.PLN, _end, It.IsAny<CancellationToken>()))
            .ReturnsAsync(110m);

        var result = await fixture.Service.GetAsync(_userId, DefaultCurrency.PLN, _start, _end, TestContext.Current.CancellationToken);

        Assert.Equal(PortfolioReturnAttributionStatus.Available, result.Status);
        Assert.Equal(10m, result.TotalChange);
        Assert.Equal(0m, result.ExternalCashMovement);
        Assert.Equal(10m, result.MarketEffect);
        Assert.Equal(0m, result.ReconciliationDifference);
    }

    [Fact]
    public async Task GetAsync_ReturnsUnavailableWhenBondCalculationDateIsMalformed()
    {
        var bondRange = new OwnerLedgerRange([], [new OwnerLedgerPoint(5, 1, _start, 1m)]);
        var details = new BondDetails(
            "Bond",
            "Issuer",
            DateOnly.FromDateTime(_start),
            DateOnly.FromDateTime(_end.AddYears(1)),
            [new BondCalculationMethod { DateValue = "not-a-date", Rate = 0m }],
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

        Assert.Equal(PortfolioReturnAttributionStatus.Unavailable, result.Status);
        Assert.Null(result.TotalChange);
    }

    [Fact]
    public async Task GetAsync_PoolsBondEntriesFromAllFormerAccountsIntoOnePosition()
    {
        // Two entries of one bond (previously two accounts): the pooled 3 units are valued once, accruing from the latest entry.
        var bondRange = new OwnerLedgerRange([], [new(5, 1, _start.AddDays(10), 1m), new(5, 2, _start.AddDays(20), 2m)]);
        var fixture = CreateFixture([], new Dictionary<long, decimal>(), new Dictionary<long, decimal>(), bondRange);
        SetupBond(fixture, DefaultCurrency.PLN);

        var result = await fixture.Service.GetAsync(_userId, DefaultCurrency.PLN, _start, _end, TestContext.Current.CancellationToken);

        Assert.Equal(PortfolioReturnAttributionStatus.Available, result.Status);
        Assert.Equal(300m, result.TotalChange);
        Assert.Equal(300m, result.ExternalCashMovement);
        Assert.Equal(0m, result.MarketEffect);
        Assert.Equal(0m, result.FxEffect);
        Assert.Equal(0m, result.ReconciliationDifference);
    }

    [Fact]
    public async Task GetAsync_CountsBondEntryPostedDuringTheEndDate()
    {
        var bondRange = new OwnerLedgerRange([], [new(5, 1, _end.AddHours(15), 1m)]);
        var fixture = CreateFixture([], new Dictionary<long, decimal>(), new Dictionary<long, decimal>(), bondRange);
        SetupBond(fixture, DefaultCurrency.PLN);

        var result = await fixture.Service.GetAsync(_userId, DefaultCurrency.PLN, _start, _end, TestContext.Current.CancellationToken);

        Assert.Equal(PortfolioReturnAttributionStatus.Available, result.Status);
        Assert.Equal(100m, result.TotalChange);
        Assert.Equal(100m, result.ExternalCashMovement);
        // The bond range covers the whole UTC day of the end date, not just its midnight.
        fixture.BondReader.Verify(x => x.GetValueRange(
            _userId, _start, _end.Date.AddDays(1).AddTicks(-1), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetAsync_AttributesForeignExchangeEffectOfBondAcrossPooledPositionChanges()
    {
        // 1 USD bond unit at the start, one more 100 days later; the rate moves 4 -> 5 -> 6.
        var middle = _start.AddDays(100);
        var bondRange = new OwnerLedgerRange([], [new(5, 1, _start, 1m), new(5, 2, middle, 1m)]);
        var fixture = CreateFixture([], new Dictionary<long, decimal>(), new Dictionary<long, decimal>(), bondRange);
        SetupBond(fixture, DefaultCurrency.USD);
        fixture.CurrencyExchange
            .Setup(x => x.GetExchangeRateAsync(
                It.Is<Currency>(currency => currency.ShortName == "USD"),
                DefaultCurrency.PLN,
                _start,
                _end,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([(_start, 4m), (middle, 5m), (_end, 6m)]);

        var result = await fixture.Service.GetAsync(_userId, DefaultCurrency.PLN, _start, _end, TestContext.Current.CancellationToken);

        Assert.Equal(PortfolioReturnAttributionStatus.Available, result.Status);
        Assert.Equal(1200m, result.TotalChange); // 2 units * 100 USD * 6
        Assert.Equal(900m, result.ExternalCashMovement); // 100 * 4 + 100 * 5
        // [start, middle): 1 unit ends at 100 USD * (5 - 4); [middle, end): 2 units end at 200 USD * (6 - 5).
        Assert.Equal(300m, result.FxEffect);
        Assert.Equal(0m, result.MarketEffect);
        Assert.Equal(0m, result.ReconciliationDifference);
    }

    [Fact]
    public async Task GetAsync_ReturnsUnavailableWhenAHeldBondHasNoDefinition()
    {
        var bondRange = new OwnerLedgerRange([], [new(7, 1, _start, 1m)]);
        var fixture = CreateFixture([], new Dictionary<long, decimal>(), new Dictionary<long, decimal>(), bondRange);

        var result = await fixture.Service.GetAsync(_userId, DefaultCurrency.PLN, _start, _end, TestContext.Current.CancellationToken);

        Assert.Equal(PortfolioReturnAttributionStatus.Unavailable, result.Status);
    }

    [Fact]
    public async Task GetAsync_UsesTheOwnersTradesAndHoldingsForNegativeGuestIds()
    {
        const int guestUserId = -4;
        var fixture = CreateFixture(
            [Flow(1, _start, InvestmentTransactionType.Buy, 1m, 100m)],
            new Dictionary<long, decimal>(),
            Holdings(1m),
            userId: guestUserId);
        fixture.PriceProvider
            .Setup(x => x.GetPricePerUnitAsync(_listingId, DefaultCurrency.PLN, _end, It.IsAny<CancellationToken>()))
            .ReturnsAsync(110m);

        var result = await fixture.Service.GetAsync(guestUserId, DefaultCurrency.PLN, _start, _end, TestContext.Current.CancellationToken);

        Assert.Equal(PortfolioReturnAttributionStatus.Available, result.Status);
        Assert.Equal(110m, result.TotalChange);
        Assert.Equal(10m, result.MarketEffect);
    }

    [Fact]
    public async Task GetAsync_WithUnsetUserId_ReturnsInsufficientDataWithoutReading()
    {
        var fixture = CreateFixture([], new Dictionary<long, decimal>(), new Dictionary<long, decimal>());

        var result = await fixture.Service.GetAsync(0, DefaultCurrency.PLN, _start, _end, TestContext.Current.CancellationToken);

        Assert.Equal(PortfolioReturnAttributionStatus.InsufficientData, result.Status);
        fixture.TransactionRepository.Verify(x => x.GetCapitalFlowInputsByUser(It.IsAny<long>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()), Times.Never);
        fixture.BondReader.Verify(x => x.GetValueRange(It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // A bond with unit value 100 whose calculation methods accrue nothing, so a pooled position is worth 100 per unit.
    private static void SetupBond(Fixture fixture, Currency currency) =>
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
                currency,
                BondType.InflationBond,
                100m)
            { Id = 5 }]);

    private static IInvestmentTransactionRepository.UserCapitalFlowInput Flow(
        long id,
        DateTime date,
        InvestmentTransactionType type,
        decimal quantity,
        decimal unitPrice,
        decimal? fee = null,
        string currency = "PLN") =>
        new(
            id,
            _listingId,
            DateOnly.FromDateTime(date),
            type,
            quantity,
            unitPrice,
            fee,
            currency);

    private static Dictionary<long, decimal> Holdings(decimal quantity) =>
        new() { [_listingId] = quantity };

    private static IReadOnlyList<InvestmentHolding> ToHoldings(IReadOnlyDictionary<long, decimal> holdings) =>
        [.. holdings.Select(holding => new InvestmentHolding(holding.Key, "TICK", "Exchange", AssetType.ETF, holding.Value))];

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
        currencyExchange
            .Setup(x => x.GetExchangeRateAsync(
                It.IsAny<Currency>(),
                It.IsAny<Currency>(),
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

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

        var service = new PortfolioReturnAttributionService(
            bondReader.Object,
            transactionRepository.Object,
            priceProvider.Object,
            new BondDashboardContext(bondDetailsRepository.Object),
            currencyExchange.Object);

        return new Fixture(service, priceProvider, currencyExchange, bondDetailsRepository, bondReader, transactionRepository);
    }

    private sealed record Fixture(
        PortfolioReturnAttributionService Service,
        Mock<IInvestmentPriceProvider> PriceProvider,
        Mock<ICurrencyExchangeService> CurrencyExchange,
        Mock<IBondDetailsRepository> BondDetailsRepository,
        Mock<IBondTransactionReader> BondReader,
        Mock<IInvestmentTransactionRepository> TransactionRepository);
}