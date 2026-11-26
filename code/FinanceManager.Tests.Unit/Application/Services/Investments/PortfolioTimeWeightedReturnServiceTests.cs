using FinanceManager.Application.Portfolio.Performance;
using FinanceManager.Application.Transactions.Bonds.Valuation;
using FinanceManager.Domain.Assets.Services;
using FinanceManager.Domain.Currencies.Entities;
using FinanceManager.Domain.Currencies.Services;
using FinanceManager.Domain.MoneyFlow.Entities;
using FinanceManager.Domain.Shared;
using FinanceManager.Domain.Transactions.Bonds.Entities;
using FinanceManager.Domain.Transactions.Bonds.Repositories;
using FinanceManager.Domain.Transactions.Investments.Entities;
using FinanceManager.Domain.Transactions.Investments.Repositories;
using FinanceManager.Domain.Transactions.Shared.Dtos;
using Moq;

namespace FinanceManager.Tests.Unit.Application.Services.Investments;

[Collection("Application")]
[Trait("Category", "Unit")]
public class PortfolioTimeWeightedReturnServiceTests
{
    private const int _userId = 1;
    private const long _listingId = 100;
    private static readonly DateTime _start = new(2023, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime _end = new(2023, 1, 3, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task GetAsync_NeutralizesContributionAndLinksDailyReturns()
    {
        var fixture = CreateFixture(
            [new IInvestmentTransactionRepository.UserCapitalFlowInput(
                1, _listingId, DateOnly.FromDateTime(_start), InvestmentTransactionType.Buy,
                1m, 100m, null, "PLN")],
            new IInvestmentTransactionRepository.UserValuationInputs(
                [], [new(_listingId, DateOnly.FromDateTime(_start), 1m)]));
        fixture.PriceProvider
            .Setup(x => x.GetPricePerUnitSeriesAsync(_listingId, DefaultCurrency.PLN, _start, _end, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Prices((100m, 100m), (101m, 110m), (102m, 110m)));

        var result = await fixture.Service.GetAsync(_userId, DefaultCurrency.PLN, _start, _end, TestContext.Current.CancellationToken);

        Assert.Equal(TimeWeightedReturnStatus.Available, result.Status);
        Assert.Equal(0.1m, result.TotalReturn);
    }

    [Fact]
    public async Task GetAsync_CarriesOpeningHoldingsIntoRangeStart()
    {
        var fixture = CreateFixture(
            [PriorBuy()],
            new IInvestmentTransactionRepository.UserValuationInputs(
                [new(_listingId, 1m)], []));
        fixture.PriceProvider
            .Setup(x => x.GetPricePerUnitSeriesAsync(_listingId, DefaultCurrency.PLN, _start, _end, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Prices((100m, 100m), (101m, 105m), (102m, 110m)));

        var result = await fixture.Service.GetAsync(_userId, DefaultCurrency.PLN, _start, _end, TestContext.Current.CancellationToken);

        Assert.Equal(TimeWeightedReturnStatus.Available, result.Status);
        Assert.Equal(0.1m, result.TotalReturn);
    }

    [Fact]
    public async Task GetAsync_ConvertsFlowsUsingHistoricalFx()
    {
        var fixture = CreateFixture(
            [new IInvestmentTransactionRepository.UserCapitalFlowInput(
                1, _listingId, DateOnly.FromDateTime(_start), InvestmentTransactionType.Buy,
                1m, 100m, null, "EUR")],
            new IInvestmentTransactionRepository.UserValuationInputs(
                [], [new(_listingId, DateOnly.FromDateTime(_start), 1m)]));
        fixture.PriceProvider
            .Setup(x => x.GetPricePerUnitSeriesAsync(_listingId, DefaultCurrency.PLN, _start, _end, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Prices((100m, 400m), (101m, 440m), (102m, 440m)));
        fixture.CurrencyExchange
            .Setup(x => x.GetExchangeRateAsync(
                It.Is<Currency>(c => c.ShortName == "EUR"), DefaultCurrency.PLN, _start, _end, It.IsAny<CancellationToken>()))
            .ReturnsAsync([(_start, 4m), (_end, 4m)]);

        var result = await fixture.Service.GetAsync(_userId, DefaultCurrency.PLN, _start, _end, TestContext.Current.CancellationToken);

        Assert.Equal(TimeWeightedReturnStatus.Available, result.Status);
        Assert.Equal(0.1m, result.TotalReturn);
    }

    [Theory]
    [InlineData("GBX", "GBP")]
    [InlineData("GBP.", "GBP")]
    [InlineData("ZAC", "ZAR")]
    [InlineData("ILA", "ILS")]
    public async Task GetAsync_NormalizesMinorTransactionCurrencyWithoutListingMultiplier(string minorCurrency, string majorCurrency)
    {
        var fixture = CreateFixture(
            [new IInvestmentTransactionRepository.UserCapitalFlowInput(
                1, _listingId, DateOnly.FromDateTime(_start), InvestmentTransactionType.Buy,
                1m, 100m, null, minorCurrency)],
            new IInvestmentTransactionRepository.UserValuationInputs(
                [], [new(_listingId, DateOnly.FromDateTime(_start), 1m)]));
        fixture.PriceProvider
            .Setup(x => x.GetPricePerUnitSeriesAsync(_listingId, DefaultCurrency.PLN, _start, _end, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Prices((100m, 5m), (101m, 5.5m), (102m, 5.5m)));
        fixture.CurrencyExchange
            .Setup(x => x.GetExchangeRateAsync(
                It.Is<Currency>(c => c.ShortName == majorCurrency), DefaultCurrency.PLN, _start, _end, It.IsAny<CancellationToken>()))
            .ReturnsAsync([(_start, 5m), (_end, 5m)]);

        var result = await fixture.Service.GetAsync(_userId, DefaultCurrency.PLN, _start, _end, TestContext.Current.CancellationToken);

        Assert.Equal(TimeWeightedReturnStatus.Available, result.Status);
        Assert.Equal(0.1m, result.TotalReturn);
    }

    [Fact]
    public async Task GetAsync_ReturnsUnavailableWhenPriceCoverageIsMissing()
    {
        var fixture = CreateFixture(
            [PriorBuy()],
            new IInvestmentTransactionRepository.UserValuationInputs(
                [new(_listingId, 1m)], []));
        fixture.PriceProvider
            .Setup(x => x.GetPricePerUnitSeriesAsync(_listingId, DefaultCurrency.PLN, _start, _end, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<DateTime, decimal> { [_start] = 100m });

        var result = await fixture.Service.GetAsync(_userId, DefaultCurrency.PLN, _start, _end, TestContext.Current.CancellationToken);

        Assert.Equal(TimeWeightedReturnStatus.Unavailable, result.Status);
        Assert.Null(result.TotalReturn);
    }

    [Fact]
    public async Task GetAsync_AggregatesSameDayFlowsDeterministically()
    {
        var flows = new[]
        {
            new IInvestmentTransactionRepository.UserCapitalFlowInput(
                2, _listingId, DateOnly.FromDateTime(_start), InvestmentTransactionType.Buy,
                1m, 100m, null, "PLN"),
            new IInvestmentTransactionRepository.UserCapitalFlowInput(
                1, _listingId, DateOnly.FromDateTime(_start), InvestmentTransactionType.Buy,
                1m, 100m, null, "PLN"),
        };
        var valuationInputs = new IInvestmentTransactionRepository.UserValuationInputs(
            [], [new(_listingId, DateOnly.FromDateTime(_start), 2m)]);
        var fixture = CreateFixture(flows, valuationInputs);
        fixture.PriceProvider
            .Setup(x => x.GetPricePerUnitSeriesAsync(_listingId, DefaultCurrency.PLN, _start, _end, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Prices((100m, 100m), (101m, 110m), (102m, 110m)));

        var first = await fixture.Service.GetAsync(_userId, DefaultCurrency.PLN, _start, _end, TestContext.Current.CancellationToken);

        var reversedFixture = CreateFixture(flows.Reverse().ToArray(), valuationInputs);
        reversedFixture.PriceProvider
            .Setup(x => x.GetPricePerUnitSeriesAsync(_listingId, DefaultCurrency.PLN, _start, _end, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Prices((100m, 100m), (101m, 110m), (102m, 110m)));
        var second = await reversedFixture.Service.GetAsync(_userId, DefaultCurrency.PLN, _start, _end, TestContext.Current.CancellationToken);

        Assert.Equal(TimeWeightedReturnStatus.Available, first.Status);
        Assert.Equal(0.1m, first.TotalReturn);
        Assert.Equal(first, second);
    }

    [Fact]
    public async Task GetAsync_UsesBondValueChangeAsContribution()
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
            null,
            new IInvestmentTransactionRepository.UserValuationInputs([], []),
            bondRange);
        fixture.BondDetailsRepository
            .Setup(x => x.GetByIdsAsync(It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([details]);

        var result = await fixture.Service.GetAsync(_userId, DefaultCurrency.PLN, _start, _end, TestContext.Current.CancellationToken);

        Assert.Equal(TimeWeightedReturnStatus.Available, result.Status);
        Assert.Equal(0m, result.TotalReturn);
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
            null,
            new IInvestmentTransactionRepository.UserValuationInputs([], []),
            bondRange);
        fixture.BondDetailsRepository
            .Setup(x => x.GetByIdsAsync(It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([details]);

        var result = await fixture.Service.GetAsync(_userId, DefaultCurrency.PLN, _start, _end, TestContext.Current.CancellationToken);

        Assert.Equal(TimeWeightedReturnStatus.Unavailable, result.Status);
        Assert.Null(result.TotalReturn);
    }

    [Fact]
    public async Task GetAsync_ValuesPooledBondEntriesOfFormerAccountsAsOnePosition()
    {
        // 1 unit at the start, 2 more the next day: contributions are neutralised, so a flat-priced bond returns 0.
        var bondRange = new OwnerLedgerRange([], [new(5, 1, _start, 1m), new(5, 2, _start.AddDays(1), 2m)]);
        var fixture = CreateFixture(null, new IInvestmentTransactionRepository.UserValuationInputs([], []), bondRange);
        SetupZeroRateBond(fixture);

        var result = await fixture.Service.GetAsync(_userId, DefaultCurrency.PLN, _start, _end, TestContext.Current.CancellationToken);

        Assert.Equal(TimeWeightedReturnStatus.Available, result.Status);
        Assert.Equal(0m, result.TotalReturn);
    }

    [Fact]
    public async Task GetAsync_CountsBondOpeningBalanceFromEntriesBeforeTheRange()
    {
        // 2 units held before the range, 1 more on the first day; nothing accrues, so the return stays 0 and flows are neutralised.
        var bondRange = new OwnerLedgerRange(
            [new OwnerLedgerPosition(5, 2m, _start.AddDays(-30))],
            [new(5, 3, _start.AddDays(1), 1m)]);
        var fixture = CreateFixture(
            [PriorBuy()],
            new IInvestmentTransactionRepository.UserValuationInputs([], []),
            bondRange);
        SetupZeroRateBond(fixture);

        var result = await fixture.Service.GetAsync(_userId, DefaultCurrency.PLN, _start, _end, TestContext.Current.CancellationToken);

        Assert.Equal(TimeWeightedReturnStatus.Available, result.Status);
        Assert.Equal(0m, result.TotalReturn);
    }

    [Fact]
    public async Task GetAsync_ReturnsUnavailableWhenAHeldBondHasNoDefinition()
    {
        var fixture = CreateFixture(
            null,
            new IInvestmentTransactionRepository.UserValuationInputs([], []),
            new OwnerLedgerRange([], [new(7, 1, _start, 1m)]));

        var result = await fixture.Service.GetAsync(_userId, DefaultCurrency.PLN, _start, _end, TestContext.Current.CancellationToken);

        Assert.Equal(TimeWeightedReturnStatus.Unavailable, result.Status);
    }

    [Fact]
    public async Task GetAsync_WithUnsetUserId_ReturnsInsufficientDataWithoutReading()
    {
        var fixture = CreateFixture(null, new IInvestmentTransactionRepository.UserValuationInputs([], []));

        var result = await fixture.Service.GetAsync(0, DefaultCurrency.PLN, _start, _end, TestContext.Current.CancellationToken);

        Assert.Equal(TimeWeightedReturnStatus.InsufficientData, result.Status);
        fixture.TransactionRepository.Verify(x => x.GetCapitalFlowInputsByUser(It.IsAny<long>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetAsync_UsesTheTradesOfANegativeGuestId()
    {
        const int guestUserId = -4;
        var fixture = CreateFixture(
            [new IInvestmentTransactionRepository.UserCapitalFlowInput(
                1, _listingId, DateOnly.FromDateTime(_start), InvestmentTransactionType.Buy, 1m, 100m, null, "PLN")],
            new IInvestmentTransactionRepository.UserValuationInputs(
                [], [new(_listingId, DateOnly.FromDateTime(_start), 1m)]),
            userId: guestUserId);
        fixture.PriceProvider
            .Setup(x => x.GetPricePerUnitSeriesAsync(_listingId, DefaultCurrency.PLN, _start, _end, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Prices((100m, 100m), (101m, 110m), (102m, 110m)));

        var result = await fixture.Service.GetAsync(guestUserId, DefaultCurrency.PLN, _start, _end, TestContext.Current.CancellationToken);

        Assert.Equal(TimeWeightedReturnStatus.Available, result.Status);
        Assert.Equal(0.1m, result.TotalReturn);
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
        IReadOnlyList<IInvestmentTransactionRepository.UserCapitalFlowInput>? flows,
        IInvestmentTransactionRepository.UserValuationInputs valuationInputs,
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
            .ReturnsAsync(flows ?? []);
        transactionRepository
            .Setup(x => x.GetValuationInputsByUser(userId, It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(valuationInputs);
        bondDetailsRepository
            .Setup(x => x.GetByIdsAsync(It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var service = new PortfolioTimeWeightedReturnService(
            bondReader.Object,
            transactionRepository.Object,
            priceProvider.Object,
            new BondDashboardContext(bondDetailsRepository.Object),
            currencyExchange.Object);

        return new Fixture(service, priceProvider, currencyExchange, bondDetailsRepository, transactionRepository);
    }

    // The opening unit comes from a trade before the period, so there is no flow inside it.
    private static IInvestmentTransactionRepository.UserCapitalFlowInput PriorBuy() =>
        new(1, _listingId, DateOnly.FromDateTime(_start.AddDays(-1)), InvestmentTransactionType.Buy, 1m, 100m, null, "PLN");

    private static IReadOnlyDictionary<DateTime, decimal> Prices(params (decimal Offset, decimal Value)[] values) =>
        values.ToDictionary(value => _start.AddDays((double)value.Offset - 100d), value => value.Value);

    private sealed record Fixture(
        PortfolioTimeWeightedReturnService Service,
        Mock<IInvestmentPriceProvider> PriceProvider,
        Mock<ICurrencyExchangeService> CurrencyExchange,
        Mock<IBondDetailsRepository> BondDetailsRepository,
        Mock<IInvestmentTransactionRepository> TransactionRepository);
}