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
public class PortfolioMoneyWeightedReturnServiceTests
{
    private const int _userId = 1;
    private const int _accountId = 10;
    private const long _listingId = 100;
    private static readonly DateTime _start = new(2023, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime _end = new(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task GetAsync_UsesEndingPortfolioValueForAnnualizedReturn()
    {
        var transaction = new IInvestmentTransactionRepository.CapitalFlowInput(
            1,
            _accountId,
            _listingId,
            DateOnly.FromDateTime(_start),
            InvestmentTransactionType.Buy,
            1m,
            100m,
            null,
            "PLN",
            null);
        var fixture = CreateFixture(
            [transaction],
            openingHoldings: new Dictionary<int, IReadOnlyDictionary<long, decimal>>(),
            endingHoldings: new Dictionary<int, IReadOnlyDictionary<long, decimal>>
            {
                [_accountId] = new Dictionary<long, decimal> { [_listingId] = 1m }
            });
        fixture.PriceProvider
            .Setup(x => x.GetPricePerUnitAsync(_listingId, DefaultCurrency.PLN, _end, It.IsAny<CancellationToken>()))
            .ReturnsAsync(110m);

        var result = await fixture.Service.GetAsync(_userId, DefaultCurrency.PLN, _start, _end, TestContext.Current.CancellationToken);

        Assert.Equal(MoneyWeightedReturnStatus.Available, result.Status);
        Assert.Equal(0.1m, result.AnnualizedReturn);
    }

    [Fact]
    public async Task GetAsync_ConvertsCashFlowsUsingHistoricalFx()
    {
        var transaction = new IInvestmentTransactionRepository.CapitalFlowInput(
            1,
            _accountId,
            _listingId,
            DateOnly.FromDateTime(_start),
            InvestmentTransactionType.Buy,
            1m,
            100m,
            null,
            "EUR",
            null);
        var fixture = CreateFixture(
            [transaction],
            openingHoldings: new Dictionary<int, IReadOnlyDictionary<long, decimal>>(),
            endingHoldings: new Dictionary<int, IReadOnlyDictionary<long, decimal>>
            {
                [_accountId] = new Dictionary<long, decimal> { [_listingId] = 1m }
            });
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
        var priorTransaction = new IInvestmentTransactionRepository.CapitalFlowInput(
            1,
            _accountId,
            _listingId,
            DateOnly.FromDateTime(_start.AddDays(-1)),
            InvestmentTransactionType.Buy,
            1m,
            100m,
            null,
            "PLN",
            null);
        var fixture = CreateFixture(
            [priorTransaction],
            openingHoldings: new Dictionary<int, IReadOnlyDictionary<long, decimal>>
            {
                [_accountId] = new Dictionary<long, decimal> { [_listingId] = 1m }
            },
            endingHoldings: new Dictionary<int, IReadOnlyDictionary<long, decimal>>
            {
                [_accountId] = new Dictionary<long, decimal> { [_listingId] = 1m }
            });
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
    public async Task GetAsync_WithNoInvestmentAccounts_ReturnsInsufficientData()
    {
        var fixture = CreateFixture(
            [],
            new Dictionary<int, IReadOnlyDictionary<long, decimal>>(),
            new Dictionary<int, IReadOnlyDictionary<long, decimal>>());

        var result = await fixture.Service.GetAsync(_userId, DefaultCurrency.PLN, _start, _end, TestContext.Current.CancellationToken);

        Assert.Equal(MoneyWeightedReturnStatus.InsufficientData, result.Status);
        Assert.Null(result.AnnualizedReturn);
    }

    [Fact]
    public async Task GetAsync_DoesNotCountRangeStartBondContributionAsOpeningValue()
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
            [],
            new Dictionary<int, IReadOnlyDictionary<long, decimal>>(),
            new Dictionary<int, IReadOnlyDictionary<long, decimal>>(),
            includeInvestmentAccount: false,
            bondAccounts: [bondAccount]);
        fixture.BondDetailsRepository
            .Setup(x => x.GetByIdsAsync(It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([details]);

        var result = await fixture.Service.GetAsync(_userId, DefaultCurrency.PLN, _start, _end, TestContext.Current.CancellationToken);

        Assert.Equal(MoneyWeightedReturnStatus.Available, result.Status);
        Assert.Equal(0m, result.AnnualizedReturn);
    }

    private static Fixture CreateFixture(
        IReadOnlyList<IInvestmentTransactionRepository.CapitalFlowInput> transactions,
        IReadOnlyDictionary<int, IReadOnlyDictionary<long, decimal>> openingHoldings,
        IReadOnlyDictionary<int, IReadOnlyDictionary<long, decimal>> endingHoldings,
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
            .ReturnsAsync(transactions);
        transactionRepository
            .Setup(x => x.GetHoldingsByAccountAsOf(It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyCollection<int> _, DateOnly asOf, CancellationToken _) =>
                asOf < DateOnly.FromDateTime(_start) ? openingHoldings : endingHoldings);
        bondDetailsRepository
            .Setup(x => x.GetByIdsAsync(It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var service = new PortfolioMoneyWeightedReturnService(
            accountRepository.Object,
            transactionRepository.Object,
            priceProvider.Object,
            new BondDashboardContext(bondDetailsRepository.Object),
            currencyExchange.Object);

        return new Fixture(service, priceProvider, currencyExchange, bondDetailsRepository);
    }

    private sealed record Fixture(
        PortfolioMoneyWeightedReturnService Service,
        Mock<IInvestmentPriceProvider> PriceProvider,
        Mock<ICurrencyExchangeService> CurrencyExchange,
        Mock<IBondDetailsRepository> BondDetailsRepository);
}