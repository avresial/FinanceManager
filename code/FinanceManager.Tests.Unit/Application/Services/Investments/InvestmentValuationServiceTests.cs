using FinanceManager.Application.FinancialAccounts.Investments.Valuation;
using FinanceManager.Domain.Assets.Services;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.FinancialAccounts.Currencies.Services;
using FinanceManager.Domain.FinancialAccounts.Investments.Entities;
using FinanceManager.Domain.FinancialAccounts.Investments.Repositories;
using FinanceManager.Domain.MoneyFlow.Services;
using Moq;

namespace FinanceManager.Tests.Unit.Application.Services.Investments;

[Trait("Category", "Unit")]
public class InvestmentValuationServiceTests
{
    private static readonly Currency _usd = new(1, "USD", "$");
    private const int _accountId = 7;

    private readonly Mock<IInvestmentTransactionRepository> _transactionRepository = new();
    private readonly Mock<IInvestmentPriceProvider> _priceProvider = new();
    private readonly Mock<IInflationIndexProvider> _inflationIndexProvider = new();
    private readonly Mock<ICurrencyExchangeService> _currencyExchangeService = new();

    private InvestmentValuationService CreateSut() =>
        new(_transactionRepository.Object, _priceProvider.Object, _inflationIndexProvider.Object, _currencyExchangeService.Object);

    private static InvestmentTransaction Tx(
        long listingId,
        InvestmentTransactionType type,
        decimal qty,
        DateOnly tradeDate,
        int accountId = _accountId,
        decimal unitPrice = 1m,
        decimal? fee = null) =>
        new()
        {
            AccountId = accountId,
            AssetListingId = listingId,
            Type = type,
            Quantity = qty,
            UnitPrice = unitPrice,
            Currency = "USD",
            TradeDate = tradeDate,
            Fee = fee
        };

    [Fact]
    public async Task GetHoldingsAsOf_OmitsZeroNetHoldings()
    {
        var asOf = new DateOnly(2024, 6, 30);
        _transactionRepository
            .Setup(x => x.GetHoldingsAsOf(It.Is<IReadOnlyCollection<int>>(a => a.Contains(_accountId)), asOf, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<long, decimal> { [10] = 5m, [20] = 0m });

        var holdings = await CreateSut().GetHoldingsAsOfAsync(_accountId, asOf, TestContext.Current.CancellationToken);

        Assert.Equal(5m, Assert.Contains(10L, holdings));
        Assert.DoesNotContain(20L, holdings);
    }

    [Fact]
    public async Task GetAccountValue_SumsHoldingsTimesPrice_AcrossListings()
    {
        var asOf = new DateTime(2024, 6, 30);
        _transactionRepository
            .Setup(x => x.GetHoldingsAsOf(It.Is<IReadOnlyCollection<int>>(a => a.Contains(_accountId)), DateOnly.FromDateTime(asOf), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<long, decimal> { [10] = 3m, [20] = 2m });
        _priceProvider.Setup(x => x.GetPricePerUnitAsync(10, _usd, asOf, It.IsAny<CancellationToken>())).ReturnsAsync(100m);
        _priceProvider.Setup(x => x.GetPricePerUnitAsync(20, _usd, asOf, It.IsAny<CancellationToken>())).ReturnsAsync(50m);

        var value = await CreateSut().GetAccountValueAsync(_accountId, _usd, asOf, TestContext.Current.CancellationToken);

        Assert.Equal(3m * 100m + 2m * 50m, value); // 400
    }

    [Fact]
    public async Task GetAccountValue_ReturnsZero_WhenAccountHoldsNothing()
    {
        var asOf = new DateTime(2024, 6, 30);
        _transactionRepository
            .Setup(x => x.GetHoldingsAsOf(It.IsAny<IReadOnlyCollection<int>>(), DateOnly.FromDateTime(asOf), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<long, decimal>());

        var value = await CreateSut().GetAccountValueAsync(_accountId, _usd, asOf, TestContext.Current.CancellationToken);

        Assert.Equal(0m, value);
        _priceProvider.Verify(
            x => x.GetPricePerUnitAsync(It.IsAny<long>(), It.IsAny<Currency>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task GetAccountValue_NetsBuyAndSell()
    {
        var asOf = new DateTime(2024, 6, 30);
        _transactionRepository
            .Setup(x => x.GetHoldingsAsOf(It.IsAny<IReadOnlyCollection<int>>(), DateOnly.FromDateTime(asOf), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<long, decimal> { [10] = 4m });
        _priceProvider.Setup(x => x.GetPricePerUnitAsync(10, _usd, asOf, It.IsAny<CancellationToken>())).ReturnsAsync(25m);

        var value = await CreateSut().GetAccountValueAsync(_accountId, _usd, asOf, TestContext.Current.CancellationToken);

        Assert.Equal(100m, value); // net holding 4 * 25
    }

    [Fact]
    public async Task GetAccountValue_ExcludesTransactionsAfterAsOf()
    {
        var asOf = new DateTime(2024, 6, 30);
        _transactionRepository
            .Setup(x => x.GetHoldingsAsOf(It.IsAny<IReadOnlyCollection<int>>(), DateOnly.FromDateTime(asOf), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<long, decimal> { [10] = 4m });
        _priceProvider.Setup(x => x.GetPricePerUnitAsync(10, _usd, asOf, It.IsAny<CancellationToken>())).ReturnsAsync(25m);

        var value = await CreateSut().GetAccountValueAsync(_accountId, _usd, asOf, TestContext.Current.CancellationToken);

        Assert.Equal(100m, value); // only the 4 units held on asOf
    }

    [Fact]
    public async Task GetAccountValue_Batched_PricesSharedListingOnce_AndReturnsPerAccount()
    {
        var asOf = new DateTime(2024, 6, 30);
        int[] accountIds = [10, 20];

        // Both accounts hold the same listing (10); account 20 also holds listing 30.
        _transactionRepository
            .Setup(x => x.GetByAccounts(It.Is<IReadOnlyCollection<int>>(a => a.Contains(10) && a.Contains(20)), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<InvestmentTransaction>
            {
                Tx(10, InvestmentTransactionType.Buy, 2m, new DateOnly(2024, 6, 1), accountId: 10),
                Tx(10, InvestmentTransactionType.Buy, 3m, new DateOnly(2024, 6, 1), accountId: 20),
                Tx(30, InvestmentTransactionType.Buy, 1m, new DateOnly(2024, 6, 1), accountId: 20)
            });
        _priceProvider.Setup(x => x.GetPricePerUnitAsync(10, _usd, asOf, It.IsAny<CancellationToken>())).ReturnsAsync(100m);
        _priceProvider.Setup(x => x.GetPricePerUnitAsync(30, _usd, asOf, It.IsAny<CancellationToken>())).ReturnsAsync(40m);

        var values = await CreateSut().GetAccountValueAsync(accountIds, _usd, asOf, TestContext.Current.CancellationToken);

        Assert.Equal(200m, values[10]); // 2 * 100
        Assert.Equal(3m * 100m + 1m * 40m, values[20]); // 340

        // The shared listing is priced once for the whole set, not once per owning account.
        _priceProvider.Verify(x => x.GetPricePerUnitAsync(10, _usd, asOf, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetAccountValue_DoesNotPriceFullyClosedPositions()
    {
        var asOf = new DateTime(2024, 6, 30);
        _transactionRepository
            .Setup(x => x.GetHoldingsAsOf(It.IsAny<IReadOnlyCollection<int>>(), DateOnly.FromDateTime(asOf), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<long, decimal> { [10] = 0m, [20] = 2m });
        _priceProvider.Setup(x => x.GetPricePerUnitAsync(20, _usd, asOf, It.IsAny<CancellationToken>())).ReturnsAsync(50m);

        var value = await CreateSut().GetAccountValueAsync(_accountId, _usd, asOf, TestContext.Current.CancellationToken);

        Assert.Equal(100m, value); // 2 * 50, closed position excluded
        _priceProvider.Verify(x => x.GetPricePerUnitAsync(10, It.IsAny<Currency>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetAccountValueSeries_CarriesHoldingsForward_AndPricesPerDay()
    {
        var start = new DateTime(2024, 1, 1); // Monday
        var end = new DateTime(2024, 1, 4);

        // Buy 2 on Jan 1, buy 1 more on Jan 3 → holding is 2 on Jan 1-2, then 3 on Jan 3-4.
        _transactionRepository
            .Setup(x => x.GetByAccounts(It.Is<IReadOnlyCollection<int>>(a => a.Contains(_accountId)), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<InvestmentTransaction>
            {
                Tx(10, InvestmentTransactionType.Buy, 2m, new DateOnly(2024, 1, 1)),
                Tx(10, InvestmentTransactionType.Buy, 1m, new DateOnly(2024, 1, 3))
            });

        // Flat price of 10 every day in the window.
        _priceProvider
            .Setup(x => x.GetPricePerUnitSeriesAsync(10, _usd, start, end, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<DateTime, decimal>
            {
                [new DateTime(2024, 1, 1)] = 10m,
                [new DateTime(2024, 1, 2)] = 10m,
                [new DateTime(2024, 1, 3)] = 10m,
                [new DateTime(2024, 1, 4)] = 10m
            });

        var series = await CreateSut().GetAccountValueSeriesAsync(_accountId, _usd, start, end, TestContext.Current.CancellationToken);

        Assert.Equal(20m, series[new DateTime(2024, 1, 1)]); // 2 * 10
        Assert.Equal(20m, series[new DateTime(2024, 1, 2)]); // holding carried forward
        Assert.Equal(30m, series[new DateTime(2024, 1, 3)]); // 3 * 10
        Assert.Equal(30m, series[new DateTime(2024, 1, 4)]);
    }

    [Fact]
    public async Task GetAccountValueSeries_SeedsHoldingsFromBeforeWindow()
    {
        var start = new DateTime(2024, 2, 1);
        var end = new DateTime(2024, 2, 2);

        // The only purchase happened before the window; the holding must be carried into it.
        _transactionRepository
            .Setup(x => x.GetByAccounts(It.Is<IReadOnlyCollection<int>>(a => a.Contains(_accountId)), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<InvestmentTransaction>
            {
                Tx(10, InvestmentTransactionType.Buy, 5m, new DateOnly(2024, 1, 15))
            });
        _priceProvider
            .Setup(x => x.GetPricePerUnitSeriesAsync(10, _usd, start, end, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<DateTime, decimal>
            {
                [new DateTime(2024, 2, 1)] = 8m,
                [new DateTime(2024, 2, 2)] = 9m
            });

        var series = await CreateSut().GetAccountValueSeriesAsync(_accountId, _usd, start, end, TestContext.Current.CancellationToken);

        Assert.Equal(40m, series[new DateTime(2024, 2, 1)]); // 5 * 8
        Assert.Equal(45m, series[new DateTime(2024, 2, 2)]); // 5 * 9
    }

    [Fact]
    public async Task GetAccountValueSeries_ReturnsEmpty_WhenNoTransactions()
    {
        _transactionRepository
            .Setup(x => x.GetByAccounts(It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<InvestmentTransaction>());

        var series = await CreateSut().GetAccountValueSeriesAsync(
            _accountId, _usd, new DateTime(2024, 1, 1), new DateTime(2024, 1, 31), TestContext.Current.CancellationToken);

        Assert.Empty(series);
    }

    [Fact]
    public async Task GetAccountValueSeries_Batched_PricesSharedListingOnce_AndMatchesPerAccount()
    {
        var start = new DateTime(2024, 1, 1);
        var end = new DateTime(2024, 1, 2);
        int[] accountIds = [10, 20];

        // Both accounts hold listing 10; account 20 also holds listing 30.
        _transactionRepository
            .Setup(x => x.GetByAccounts(It.Is<IReadOnlyCollection<int>>(a => a.Contains(10) && a.Contains(20)), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<InvestmentTransaction>
            {
                Tx(10, InvestmentTransactionType.Buy, 2m, new DateOnly(2024, 1, 1), accountId: 10),
                Tx(10, InvestmentTransactionType.Buy, 3m, new DateOnly(2024, 1, 1), accountId: 20),
                Tx(30, InvestmentTransactionType.Buy, 1m, new DateOnly(2024, 1, 1), accountId: 20)
            });

        _priceProvider
            .Setup(x => x.GetPricePerUnitSeriesAsync(10, _usd, start, end, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<DateTime, decimal> { [start] = 10m, [end] = 12m });
        _priceProvider
            .Setup(x => x.GetPricePerUnitSeriesAsync(30, _usd, start, end, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<DateTime, decimal> { [start] = 40m, [end] = 40m });

        var byAccount = await CreateSut().GetAccountValueSeriesAsync(accountIds, _usd, start, end, TestContext.Current.CancellationToken);

        Assert.Equal(20m, byAccount[10][start]);  // 2 * 10
        Assert.Equal(24m, byAccount[10][end]);    // 2 * 12
        Assert.Equal(3m * 10m + 40m, byAccount[20][start]); // 70
        Assert.Equal(3m * 12m + 40m, byAccount[20][end]);   // 76

        // The listing shared by both accounts is priced once across the whole set.
        _priceProvider.Verify(x => x.GetPricePerUnitSeriesAsync(10, _usd, start, end, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetCapitalSeries_AccumulatesBuysAndSells_CarriesOpeningCapital_AndCombinesSameDayFlows()
    {
        var start = new DateTime(2024, 1, 2);
        var end = new DateTime(2024, 1, 4);
        SetupTransactions(
            Tx(1, InvestmentTransactionType.Buy, 10m, new DateOnly(2024, 1, 1), unitPrice: 10m, fee: 2m),
            Tx(1, InvestmentTransactionType.Buy, 2m, new DateOnly(2024, 1, 2), unitPrice: 10m, fee: 1m),
            Tx(1, InvestmentTransactionType.Buy, 1m, new DateOnly(2024, 1, 2), unitPrice: 5m),
            Tx(1, InvestmentTransactionType.Sell, 1m, new DateOnly(2024, 1, 3), unitPrice: 15m, fee: 1m));

        var series = await CreateSut().GetCapitalSeriesAsync(
            [_accountId], _usd, start, end, TestContext.Current.CancellationToken);

        Assert.Equal(128m, series[_accountId][start]);
        Assert.Equal(114m, series[_accountId][start.AddDays(1)]);
        Assert.Equal(114m, series[_accountId][end]);
        Assert.Equal(114m, series[_accountId][start.AddDays(2)]);
    }

    [Fact]
    public async Task GetCapitalSeries_ConvertsEachCashFlowUsingOneHistoricalRateRange()
    {
        var start = new DateTime(2024, 1, 2);
        var end = new DateTime(2024, 1, 3);
        var eur = new Currency(2, "EUR", "€");
        var transactions = new[]
        {
            Tx(1, InvestmentTransactionType.Buy, 2m, new DateOnly(2024, 1, 2), unitPrice: 100m),
            Tx(1, InvestmentTransactionType.Sell, 1m, new DateOnly(2024, 1, 3), unitPrice: 50m)
        };
        foreach (var transaction in transactions)
            transaction.Currency = eur.ShortName;
        SetupTransactions(transactions);

        _currencyExchangeService
            .Setup(x => x.GetExchangeRateAsync(
                It.Is<Currency>(currency => currency.ShortName == "EUR"),
                It.Is<Currency>(currency => currency.ShortName == "PLN"),
                start,
                end))
            .ReturnsAsync([
                (start, (decimal?)2m),
                (end, (decimal?)3m)
            ]);

        var series = await CreateSut().GetCapitalSeriesAsync(
            [_accountId], DefaultCurrency.PLN, start, end, TestContext.Current.CancellationToken);

        Assert.Equal(400m, series[_accountId][start]);
        Assert.Equal(250m, series[_accountId][end]);
        _currencyExchangeService.Verify(
            x => x.GetExchangeRateAsync(
                It.Is<Currency>(currency => currency.ShortName == "EUR"),
                It.Is<Currency>(currency => currency.ShortName == "PLN"),
                start,
                end),
            Times.Once);
    }

    [Fact]
    public async Task GetCapitalSeries_IgnoresUnknownFutureTransactionTypes()
    {
        var transactions = new[]
        {
            Tx(1, (InvestmentTransactionType)999, 5m, new DateOnly(2024, 1, 1), unitPrice: 100m)
        };
        SetupTransactions(transactions);

        var series = await CreateSut().GetCapitalSeriesAsync(
            [_accountId], _usd, new DateTime(2024, 1, 1), new DateTime(2024, 1, 2), TestContext.Current.CancellationToken);

        Assert.Empty(series);
        _currencyExchangeService.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task GetBenchmarkSeries_DefaultsToInflation_AndOpensAtTheCarriedInHolding()
    {
        var start = new DateTime(2024, 1, 1);
        var end = new DateTime(2024, 3, 1);
        // 10 units bought before the range opens, worth 100 each on the first day.
        SetupTransactions(Tx(1, InvestmentTransactionType.Buy, 10m, new DateOnly(2023, 12, 1)));
        SetupPrices(1, new Dictionary<DateTime, decimal> { [start] = 100m, [end] = 130m });
        _inflationIndexProvider
            .Setup(x => x.GetIndexSeriesAsync(start, end, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<DateTime, decimal>
            {
                [start] = 100m,
                [end] = 105m
            });

        var series = await CreateSut().GetBenchmarkSeriesAsync(
            null, _accountId, _usd, start, end, TestContext.Current.CancellationToken);

        Assert.Equal(1_000m, series[start]);
        Assert.Equal(1_050m, series[end]);
    }

    [Fact]
    public async Task GetBenchmarkSeries_AbsorbsContributionsMadeDuringTheRange()
    {
        var start = new DateTime(2024, 1, 1);
        var mid = new DateTime(2024, 1, 2);
        var end = new DateTime(2024, 1, 3);
        // Nothing carried in; one 500 purchase on the middle day.
        SetupTransactions(Tx(1, InvestmentTransactionType.Buy, 5m, new DateOnly(2024, 1, 2)));
        SetupPrices(1, new Dictionary<DateTime, decimal> { [start] = 100m, [mid] = 100m, [end] = 100m });
        _inflationIndexProvider
            .Setup(x => x.GetIndexSeriesAsync(start, end, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<DateTime, decimal>
            {
                [start] = 100m,
                [mid] = 100m,
                [end] = 110m
            });

        var series = await CreateSut().GetBenchmarkSeriesAsync(
            null, _accountId, _usd, start, end, TestContext.Current.CancellationToken);

        // Before the purchase the shadow portfolio holds nothing, so the day is omitted entirely.
        Assert.DoesNotContain(start, series.Keys);
        Assert.Equal(500m, series[mid]);
        // The contribution rides the index from its own trade date: 500 * 110/100.
        Assert.Equal(550m, series[end]);
    }

    [Fact]
    public async Task GetBenchmarkSeries_TracksMoneyWithdrawnByASell()
    {
        var start = new DateTime(2024, 1, 1);
        var end = new DateTime(2024, 1, 2);
        SetupTransactions(
            Tx(1, InvestmentTransactionType.Buy, 10m, new DateOnly(2023, 12, 1)),
            Tx(1, InvestmentTransactionType.Sell, 4m, new DateOnly(2024, 1, 2)));
        SetupPrices(1, new Dictionary<DateTime, decimal> { [start] = 100m, [end] = 100m });
        _inflationIndexProvider
            .Setup(x => x.GetIndexSeriesAsync(start, end, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<DateTime, decimal> { [start] = 100m, [end] = 100m });

        var series = await CreateSut().GetBenchmarkSeriesAsync(
            null, _accountId, _usd, start, end, TestContext.Current.CancellationToken);

        Assert.Equal(1_000m, series[start]);
        Assert.Equal(600m, series[end]); // 400 of the 1,000 withdrawn on the sell date
    }

    [Fact]
    public async Task GetBenchmarkSeries_CarriesTheLastPublishedIndexLevelAcrossGaps()
    {
        var start = new DateTime(2024, 1, 1);
        var end = new DateTime(2024, 1, 3);
        SetupTransactions(Tx(1, InvestmentTransactionType.Buy, 10m, new DateOnly(2023, 12, 1)));
        SetupPrices(1, new Dictionary<DateTime, decimal> { [start] = 100m });
        // Inflation only publishes monthly, so the range's later days have no point of their own.
        _inflationIndexProvider
            .Setup(x => x.GetIndexSeriesAsync(start, end, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<DateTime, decimal> { [start] = 100m });

        var series = await CreateSut().GetBenchmarkSeriesAsync(
            null, _accountId, _usd, start, end, TestContext.Current.CancellationToken);

        Assert.Equal(3, series.Count);
        Assert.Equal(1_000m, series[end]);
    }

    [Fact]
    public async Task GetBenchmarkSeries_UsesSelectedAvailableInvestment()
    {
        var start = new DateTime(2024, 1, 1);
        var end = new DateTime(2024, 1, 2);
        SetupTransactions(Tx(1, InvestmentTransactionType.Buy, 10m, new DateOnly(2023, 12, 1)));
        SetupPrices(1, new Dictionary<DateTime, decimal> { [start] = 100m, [end] = 100m });
        SetupPrices(42, new Dictionary<DateTime, decimal> { [start] = 50m, [end] = 60m });

        var series = await CreateSut().GetBenchmarkSeriesAsync(
            42, _accountId, _usd, start, end, TestContext.Current.CancellationToken);

        Assert.Equal(1_000m, series[start]);
        Assert.Equal(1_200m, series[end]); // 1,000 * 60/50
        _inflationIndexProvider.Verify(
            x => x.GetIndexSeriesAsync(
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task GetBenchmarkSeries_BenchmarkingAgainstAHeldInstrument_PricesItOnce()
    {
        var start = new DateTime(2024, 1, 1);
        var end = new DateTime(2024, 1, 2);
        SetupTransactions(Tx(42, InvestmentTransactionType.Buy, 10m, new DateOnly(2023, 12, 1)));
        SetupPrices(42, new Dictionary<DateTime, decimal> { [start] = 50m, [end] = 60m });

        var series = await CreateSut().GetBenchmarkSeriesAsync(
            42, _accountId, _usd, start, end, TestContext.Current.CancellationToken);

        Assert.Equal(500m, series[start]);
        Assert.Equal(600m, series[end]);
        _priceProvider.Verify(
            x => x.GetPricePerUnitSeriesAsync(42, _usd, start, end, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetBenchmarkSeries_ForAnAccountWithoutTransactions_IsEmpty()
    {
        var start = new DateTime(2024, 1, 1);
        var end = new DateTime(2024, 1, 2);
        SetupTransactions();

        var series = await CreateSut().GetBenchmarkSeriesAsync(
            null, _accountId, _usd, start, end, TestContext.Current.CancellationToken);

        Assert.Empty(series);
    }

    private void SetupTransactions(params InvestmentTransaction[] transactions) =>
        _transactionRepository
            .Setup(x => x.GetByAccounts(It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(transactions);

    private void SetupPrices(long listingId, Dictionary<DateTime, decimal> prices) =>
        _priceProvider
            .Setup(x => x.GetPricePerUnitSeriesAsync(
                listingId, _usd, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(prices);
}