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
public class PortfolioReturnAttributionServiceTests
{
    private const int _userId = 1;
    private const int _accountId = 10;
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
            openingHoldings: new Dictionary<int, IReadOnlyDictionary<long, decimal>>(),
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
            endingHoldings: new Dictionary<int, IReadOnlyDictionary<long, decimal>>());
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
            openingHoldings: new Dictionary<int, IReadOnlyDictionary<long, decimal>>(),
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
            openingHoldings: new Dictionary<int, IReadOnlyDictionary<long, decimal>>(),
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
    public async Task GetAsync_MissingHistoricalExchangeRateReturnsUnavailable()
    {
        var transaction = Flow(1, _start, InvestmentTransactionType.Buy, 1m, 100m, currency: "EUR");
        var fixture = CreateFixture(
            [transaction],
            openingHoldings: new Dictionary<int, IReadOnlyDictionary<long, decimal>>(),
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

    private static IInvestmentTransactionRepository.CapitalFlowInput Flow(
        long id,
        DateTime date,
        InvestmentTransactionType type,
        decimal quantity,
        decimal unitPrice,
        decimal? fee = null,
        string currency = "PLN") =>
        new(
            id,
            _accountId,
            _listingId,
            DateOnly.FromDateTime(date),
            type,
            quantity,
            unitPrice,
            fee,
            currency,
            null);

    private static Dictionary<int, IReadOnlyDictionary<long, decimal>> Holdings(decimal quantity) =>
        new()
        {
            [_accountId] = new Dictionary<long, decimal> { [_listingId] = quantity },
        };

    private static Fixture CreateFixture(
        IReadOnlyList<IInvestmentTransactionRepository.CapitalFlowInput> transactions,
        IReadOnlyDictionary<int, IReadOnlyDictionary<long, decimal>> openingHoldings,
        IReadOnlyDictionary<int, IReadOnlyDictionary<long, decimal>> endingHoldings)
    {
        var accountRepository = new Mock<IFinancialAccountRepository>();
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
        var investmentAccount = new InvestmentAccount(_userId, _accountId, "Investments");

        accountRepository
            .Setup(x => x.GetAccounts<InvestmentAccount>(_userId, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<bool>()))
            .Returns(new[] { investmentAccount }.ToAsyncEnumerable());
        accountRepository
            .Setup(x => x.GetAccounts<BondAccount>(_userId, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<bool>()))
            .Returns(Array.Empty<BondAccount>().ToAsyncEnumerable());
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

        var service = new PortfolioReturnAttributionService(
            accountRepository.Object,
            transactionRepository.Object,
            priceProvider.Object,
            new BondDashboardContext(bondDetailsRepository.Object),
            currencyExchange.Object);

        return new Fixture(service, priceProvider, currencyExchange);
    }

    private sealed record Fixture(
        PortfolioReturnAttributionService Service,
        Mock<IInvestmentPriceProvider> PriceProvider,
        Mock<ICurrencyExchangeService> CurrencyExchange);
}