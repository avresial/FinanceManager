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
using FinanceManager.Domain.MoneyFlow.Entities;
using FinanceManager.Domain.Shared;
using Moq;

namespace FinanceManager.Tests.Unit.Application.Services.Investments;

[Collection("Application")]
[Trait("Category", "Unit")]
public class PortfolioTimeWeightedReturnServiceTests
{
    private const int _userId = 1;
    private const int _accountId = 10;
    private const long _listingId = 100;
    private static readonly DateTime _start = new(2023, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime _end = new(2023, 1, 3, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task GetAsync_NeutralizesContributionAndLinksDailyReturns()
    {
        var fixture = CreateFixture(
            [new IInvestmentTransactionRepository.CapitalFlowInput(
                1, _accountId, _listingId, DateOnly.FromDateTime(_start), InvestmentTransactionType.Buy,
                1m, 100m, null, "PLN", null)],
            new IInvestmentTransactionRepository.AccountValuationInputs(
                [], [new(_accountId, _listingId, DateOnly.FromDateTime(_start), 1m)]));
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
            null,
            new IInvestmentTransactionRepository.AccountValuationInputs(
                [new(_accountId, _listingId, 1m)], []));
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
            [new IInvestmentTransactionRepository.CapitalFlowInput(
                1, _accountId, _listingId, DateOnly.FromDateTime(_start), InvestmentTransactionType.Buy,
                1m, 100m, null, "EUR", null)],
            new IInvestmentTransactionRepository.AccountValuationInputs(
                [], [new(_accountId, _listingId, DateOnly.FromDateTime(_start), 1m)]));
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

    [Fact]
    public async Task GetAsync_ReturnsUnavailableWhenPriceCoverageIsMissing()
    {
        var fixture = CreateFixture(
            null,
            new IInvestmentTransactionRepository.AccountValuationInputs(
                [new(_accountId, _listingId, 1m)], []));
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
            new IInvestmentTransactionRepository.CapitalFlowInput(
                2, _accountId, _listingId, DateOnly.FromDateTime(_start), InvestmentTransactionType.Buy,
                1m, 100m, null, "PLN", null),
            new IInvestmentTransactionRepository.CapitalFlowInput(
                1, _accountId, _listingId, DateOnly.FromDateTime(_start), InvestmentTransactionType.Buy,
                1m, 100m, null, "PLN", null),
        };
        var valuationInputs = new IInvestmentTransactionRepository.AccountValuationInputs(
            [], [new(_accountId, _listingId, DateOnly.FromDateTime(_start), 2m)]);
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
        var bondAccount = new BondAccount(
            _userId,
            20,
            "Bonds",
            [new BondAccountEntry(20, 1, _start, 1m, 1m, 5)]);
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
            new IInvestmentTransactionRepository.AccountValuationInputs([], []),
            includeInvestmentAccount: false,
            bondAccounts: [bondAccount]);
        fixture.BondDetailsRepository
            .Setup(x => x.GetByIdsAsync(It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([details]);

        var result = await fixture.Service.GetAsync(_userId, DefaultCurrency.PLN, _start, _end, TestContext.Current.CancellationToken);

        Assert.Equal(TimeWeightedReturnStatus.Available, result.Status);
        Assert.Equal(0m, result.TotalReturn);
    }

    private static Fixture CreateFixture(
        IReadOnlyList<IInvestmentTransactionRepository.CapitalFlowInput>? flows,
        IInvestmentTransactionRepository.AccountValuationInputs valuationInputs,
        bool includeInvestmentAccount = true,
        IReadOnlyList<BondAccount>? bondAccounts = null)
    {
        var accountRepository = new Mock<IFinancialAccountRepository>();
        var transactionRepository = new Mock<IInvestmentTransactionRepository>();
        var priceProvider = new Mock<IInvestmentPriceProvider>();
        var bondDetailsRepository = new Mock<IBondDetailsRepository>();
        var currencyExchange = new Mock<ICurrencyExchangeService>();
        var investmentAccount = new InvestmentAccount(_userId, _accountId, "Investments");

        accountRepository
            .Setup(x => x.GetAccounts<InvestmentAccount>(_userId, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<bool>()))
            .Returns((includeInvestmentAccount ? new[] { investmentAccount } : Array.Empty<InvestmentAccount>()).ToAsyncEnumerable());
        accountRepository
            .Setup(x => x.GetAccounts<BondAccount>(_userId, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<bool>()))
            .Returns((bondAccounts ?? []).ToAsyncEnumerable());
        transactionRepository
            .Setup(x => x.GetCapitalFlowInputs(It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(flows ?? []);
        transactionRepository
            .Setup(x => x.GetValuationInputs(It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(valuationInputs);
        bondDetailsRepository
            .Setup(x => x.GetByIdsAsync(It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var service = new PortfolioTimeWeightedReturnService(
            accountRepository.Object,
            transactionRepository.Object,
            priceProvider.Object,
            new BondDashboardContext(bondDetailsRepository.Object),
            currencyExchange.Object);

        return new Fixture(service, priceProvider, currencyExchange, bondDetailsRepository);
    }

    private static IReadOnlyDictionary<DateTime, decimal> Prices(params (decimal Offset, decimal Value)[] values) =>
        values.ToDictionary(value => _start.AddDays((double)value.Offset - 100d), value => value.Value);

    private sealed record Fixture(
        PortfolioTimeWeightedReturnService Service,
        Mock<IInvestmentPriceProvider> PriceProvider,
        Mock<ICurrencyExchangeService> CurrencyExchange,
        Mock<IBondDetailsRepository> BondDetailsRepository);
}