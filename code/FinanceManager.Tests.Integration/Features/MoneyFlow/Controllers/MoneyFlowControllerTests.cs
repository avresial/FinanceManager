using FinanceManager.Components.Features.MoneyFlow.HttpClients;
using FinanceManager.Domain.Assets.Entities;
using FinanceManager.Domain.FinancialAccounts.Bond.Entities;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.FinancialAccounts.Currencies.Repositories;
using FinanceManager.Domain.FinancialAccounts.Investments.Dtos;
using FinanceManager.Domain.FinancialAccounts.Investments.Entities;
using FinanceManager.Domain.FinancialAccounts.Shared.Commands;
using FinanceManager.Domain.FinancialAccounts.Shared.Dtos;
using FinanceManager.Domain.FinancialAccounts.Shared.Entities;
using FinanceManager.Domain.Identity.Entities;
using FinanceManager.Domain.Identity.Repositories;
using FinanceManager.Domain.Shared;
using FinanceManager.Infrastructure.Persistence;
using FinanceManager.Tests.Integration.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace FinanceManager.Tests.Integration.Features.MoneyFlow.Controllers;

[Trait("Category", "Integration")]
public class MoneyFlowControllerTests(OptionsProvider optionsProvider) : ControllerTests(optionsProvider), IDisposable
{
    private TestDatabase _testDatabase = null!;
    private DateTime _nowUtc;
    private int _valueChange = 10;
    private int _value;

    protected override bool ReuseApplicationHost => false;

    protected override void ConfigureServices(IServiceCollection services)
    {
        _nowUtc = DateTime.UtcNow.Date;
        _testDatabase = new TestDatabase();

        // remove any registration for AppDbContext
        var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));
        if (descriptor != null)
            services.Remove(descriptor);

        services.AddSingleton(_testDatabase.Context);

#pragma warning disable CS0854
        var currencyRepoMock = new Mock<ICurrencyRepository>();
        currencyRepoMock.Setup(x => x.GetCurrencies(It.IsAny<CancellationToken>())).Returns(new[] { DefaultCurrency.PLN, DefaultCurrency.USD }.ToAsyncEnumerable());
#pragma warning restore CS0854

        services.AddSingleton(currencyRepoMock.Object);
    }

    private static BondDetails CreateBondDetails(int id, DateTime nowUtc, decimal unitValue = 1m)
    {
        var calculationMethod = new BondCalculationMethod
        {
            Id = id,
            DateOperator = DateOperator.UntilDate,
            DateValue = DateOnly.FromDateTime(nowUtc.AddYears(1)).ToString("yyyy-MM-dd"),
            Rate = 0.0365m
        };

        return new BondDetails(
            $"Bond {id}",
            "Test Issuer",
            DateOnly.FromDateTime(nowUtc.AddYears(-1)),
            DateOnly.FromDateTime(nowUtc.AddYears(5)),
            [calculationMethod],
            unitValue: unitValue)
        { Id = id };
    }

    private async Task SeedCashEntries()
    {
        FinancialLabel salary = new() { Name = "salary" };
        _testDatabase.Context.FinancialLabels.Add(salary);
        _value = 0;

        await _testDatabase.Context.SaveChangesAsync(TestContext.Current.CancellationToken);

        for (DateTime i = _nowUtc.AddMonths(-2).Date; i <= _nowUtc; i = i.AddDays(1))
            _testDatabase.Context.CurrencyEntries.Add(new CurrencyAccountEntry(null, 0, i, _value += _valueChange, _valueChange)
            {
                UserId = 1,
                CurrencyId = DefaultCurrency.PLN.Id,
                Labels = [salary]
            });

        await _testDatabase.Context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task GetNetWorth_SingleDate_ReturnsValue()
    {
        await SeedCashEntries();
        Authorize("TestUser", 1, UserRole.User);

        var result = await Client.GetFromJsonAsync<decimal?>($"api/MoneyFlow/GetNetWorth/1/{DefaultCurrency.USD.Id}/{_nowUtc:O}", TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.Equal(_value, result);
    }

    [Fact]
    public async Task GetNetWorth_Range_ReturnsDictionary()
    {
        await SeedCashEntries();
        Authorize("TestUser", 1, UserRole.User);

        var result = await new MoneyFlowHttpClient(Client).GetNetWorth(1, DefaultCurrency.USD, _nowUtc.AddDays(-7), _nowUtc);

        Assert.NotNull(result);
        Assert.NotEmpty(result);
    }

    [Fact]
    public async Task GetInflow_ReturnsList()
    {
        await SeedCashEntries();
        Authorize("TestUser", 1, UserRole.User);
        int days = 7;

        var result = await new MoneyFlowHttpClient(Client).GetInflow(1, DefaultCurrency.USD, _nowUtc.AddDays(-days), _nowUtc);

        Assert.NotNull(result);
        Assert.All(result, x => Assert.Equal(_valueChange, x.Value));
    }

    [Fact]
    public async Task GetOutflow_ReturnsList()
    {
        await SeedCashEntries();
        Authorize("TestUser", 1, UserRole.User);

        var result = await new MoneyFlowHttpClient(Client).GetOutflow(1, DefaultCurrency.USD, _nowUtc.AddDays(-7), _nowUtc);

        Assert.NotNull(result);
        Assert.Equal(0, result.Sum(x => x.Value));
    }

    [Fact]
    public async Task GetNetCashFlow_ReturnsList()
    {
        await SeedCashEntries();
        Authorize("TestUser", 1, UserRole.User);
        int days = 7;

        var result = await new MoneyFlowHttpClient(Client).GetNetCashFlow(1, DefaultCurrency.USD, _nowUtc.AddDays(-days), _nowUtc);

        Assert.NotNull(result);
        Assert.All(result, x => Assert.Equal(_valueChange, x.Value));
        Assert.Equal((days + 1) * _valueChange, result.Sum(x => x.Value));
    }

    [Fact]
    public async Task GetClosingBalance_ReturnsDailyClosingBalances()
    {
        await SeedCashEntries();
        Authorize("TestUser", 1, UserRole.User);
        int days = 7;

        var result = await new MoneyFlowHttpClient(Client).GetClosingBalance(1, DefaultCurrency.USD, _nowUtc.AddDays(-days), _nowUtc);

        Assert.NotNull(result);
        Assert.Equal(days + 1, result.Count);
        Assert.Equal(_value - (days * _valueChange), result.First(x => x.DateTime == _nowUtc.AddDays(-days)).Value);
        Assert.Equal(_value, result.First(x => x.DateTime == _nowUtc).Value);
    }

    [Fact]
    public async Task GetLabelsValue_ReturnsList()
    {
        await SeedCashEntries();
        Authorize("TestUser", 1, UserRole.User);
        int days = 7;

        var result = await new MoneyFlowHttpClient(Client).GetLabelsValue(1, _nowUtc.AddDays(-days), _nowUtc);

        Assert.NotNull(result);
        Assert.Equal((1 + days) * _valueChange, result.First().Value);
    }

    [Fact]
    public async Task GetSavingsRate_ReturnsList()
    {
        await SeedCashEntries();
        Authorize("TestUser", 1, UserRole.User);
        var start = _nowUtc.AddDays(-7);
        var url = $"api/MoneyFlow/GetSavingsRate?userId=1&currencyId={DefaultCurrency.PLN.Id}&start={start:O}&end={_nowUtc:O}";

        var result = await Client.GetFromJsonAsync<List<FinanceManager.Domain.MoneyFlow.Entities.SavingsRate>>(url, TestContext.Current.CancellationToken);

        var rate = Assert.Single(result!);
        Assert.Equal(8 * _valueChange, rate.Salary);
        Assert.Equal(0m, rate.Spending);
    }

    [Fact]
    public async Task LabelSavingsAndInvestmentReports_CountEntriesThatAreNotLinkedToAnAccount()
    {
        // Entries without an account belong to their owner; another owner's entry and an entry without a currency never count.
        FinancialLabel salary = new() { Name = "Salary" };
        _testDatabase.Context.FinancialLabels.Add(salary);
        await _testDatabase.Context.SaveChangesAsync(TestContext.Current.CancellationToken);
        _testDatabase.Context.CurrencyEntries.AddRange(
            new CurrencyAccountEntry(null, 0, _nowUtc.AddDays(-2), 0, 1000m) { Labels = [salary], UserId = 1, CurrencyId = DefaultCurrency.PLN.Id },
            new CurrencyAccountEntry(null, 0, _nowUtc.AddDays(-1), 0, -200m) { Labels = [], UserId = 1, CurrencyId = DefaultCurrency.PLN.Id },
            new CurrencyAccountEntry(null, 0, _nowUtc.AddDays(-1), 0, 5000m) { Labels = [salary], UserId = 2, CurrencyId = DefaultCurrency.PLN.Id });
        await _testDatabase.Context.SaveChangesAsync(TestContext.Current.CancellationToken);
        Authorize("TestUser", 1, UserRole.User);
        var client = new MoneyFlowHttpClient(Client);
        var start = _nowUtc.AddDays(-7);

        var labels = await client.GetLabelsValue(1, start, _nowUtc);
        var savings = Assert.Single((await Client.GetFromJsonAsync<List<FinanceManager.Domain.MoneyFlow.Entities.SavingsRate>>(
            $"api/MoneyFlow/GetSavingsRate?userId=1&currencyId={DefaultCurrency.PLN.Id}&start={start:O}&end={_nowUtc:O}", TestContext.Current.CancellationToken))!);
        var investment = Assert.Single(await client.GetInvestmentRate(1, DefaultCurrency.PLN, start, _nowUtc).ToListAsync(TestContext.Current.CancellationToken));

        Assert.Equal(1000m, labels.Single(x => x.Name == "Salary").Value);
        Assert.Equal(1000m, savings.Salary);
        Assert.Equal(200m, savings.Spending);
        Assert.Equal(1000m, investment.Salary);
    }

    [Fact]
    public async Task GetInvestmentRate_ReturnsList()
    {
        await SeedCashEntries();
        Authorize("TestUser", 1, UserRole.User);
        int days = 7;

        var result = await new MoneyFlowHttpClient(Client).GetInvestmentRate(1, DefaultCurrency.USD, _nowUtc.AddDays(-days), _nowUtc).ToListAsync(TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.NotEmpty(result);
        Assert.Equal((1 + days) * _valueChange, result.First().Salary);
    }

    [Fact]
    public async Task GetNetWorth_CashAndBondEntries_ReturnAggregatedValue()
    {
        await SeedCashEntries();

        _testDatabase.Context.CurrencyEntries.Add(
            new CurrencyAccountEntry(null, 100, _nowUtc.AddDays(-5), 5000m, 5000m) { UserId = 1, CurrencyId = DefaultCurrency.PLN.Id, Labels = [] });
        await _testDatabase.Context.SaveChangesAsync(TestContext.Current.CancellationToken);

        _testDatabase.Context.Bonds.Add(CreateBondDetails(1, _nowUtc));
        await _testDatabase.Context.SaveChangesAsync(TestContext.Current.CancellationToken);

        _testDatabase.Context.BondEntries.Add(
            new BondAccountEntry(null, 300, _nowUtc.AddDays(-10), 3000m, 3000m, 1) { UserId = 1 });
        await _testDatabase.Context.SaveChangesAsync(TestContext.Current.CancellationToken);

        Authorize("TestUser", 1, UserRole.User);

        // Act
        var result = await Client.GetFromJsonAsync<decimal?>($"api/MoneyFlow/GetNetWorth/1/{DefaultCurrency.USD.Id}/{_nowUtc:O}", TestContext.Current.CancellationToken);

        // Assert - Should aggregate all account types
        Assert.NotNull(result);

        // Result should be > 0 and include contributions from all accounts
        // Exact value depends on stock price provider mock
        Assert.True(result > 8000m, $"Expected net worth > 8000 (currency + bonds alone), got {result}");
    }

    [Fact]
    public async Task GetNetWorth_LargePortfolio_PerformsEfficiently()
    {

        // Add 500 entries over time
        for (int i = 0; i < 500; i++)
        {
            _testDatabase.Context.CurrencyEntries.Add(
                new CurrencyAccountEntry(null, i + 1, _nowUtc.AddDays(-500 + i), 0, 100m) { UserId = 1, CurrencyId = DefaultCurrency.PLN.Id, Labels = [] });
        }
        await _testDatabase.Context.SaveChangesAsync(TestContext.Current.CancellationToken);

        Authorize("TestUser", 1, UserRole.User);

        // The first request through the test host pays for pipeline and EF query-plan JIT, which on a
        // cold CI runner alone can approach the budget below. Warm it up so the measurement reflects
        // the cost of the query over 500 entries, which is what this test is guarding.
        await Client.GetFromJsonAsync<decimal?>($"api/MoneyFlow/GetNetWorth/1/{DefaultCurrency.USD.Id}/{_nowUtc:O}", TestContext.Current.CancellationToken);

        // Act - Measure performance
        var startTime = DateTime.UtcNow;
        var result = await Client.GetFromJsonAsync<decimal?>($"api/MoneyFlow/GetNetWorth/1/{DefaultCurrency.USD.Id}/{_nowUtc:O}", TestContext.Current.CancellationToken);
        var duration = DateTime.UtcNow - startTime;

        // Assert
        Assert.NotNull(result);
        Assert.True(result > 0);

        // Should complete in reasonable time (under 5 seconds for large dataset)
        Assert.True(duration.TotalSeconds < 5,
            $"GetNetWorth took {duration.TotalSeconds}s, expected < 5s");
    }

    [Fact]
    public async Task GetNetWorth_RangeWithMultipleAccountChanges_ReturnsCorrectTimeSeries()
    {
        // Arrange - Account with changing values over time
        // Add entries with varying value changes
        _testDatabase.Context.CurrencyEntries.Add(
            new CurrencyAccountEntry(null, 1, _nowUtc.AddDays(-10), 1000m, 1000m) { UserId = 1, CurrencyId = DefaultCurrency.PLN.Id, Labels = [] });
        _testDatabase.Context.CurrencyEntries.Add(
            new CurrencyAccountEntry(null, 2, _nowUtc.AddDays(-7), 1500m, 500m) { UserId = 1, CurrencyId = DefaultCurrency.PLN.Id, Labels = [] });
        _testDatabase.Context.CurrencyEntries.Add(
            new CurrencyAccountEntry(null, 3, _nowUtc.AddDays(-5), 1200m, -300m) { UserId = 1, CurrencyId = DefaultCurrency.PLN.Id, Labels = [] });
        _testDatabase.Context.CurrencyEntries.Add(
            new CurrencyAccountEntry(null, 4, _nowUtc.AddDays(-2), 1400m, 200m) { UserId = 1, CurrencyId = DefaultCurrency.PLN.Id, Labels = [] });

        await _testDatabase.Context.SaveChangesAsync(TestContext.Current.CancellationToken);

        Authorize("TestUser", 1, UserRole.User);

        // Act
        var result = await new MoneyFlowHttpClient(Client).GetNetWorth(
            1, DefaultCurrency.USD, _nowUtc.AddDays(-10), _nowUtc);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(11, result.Count); // 11 days from -10 to 0

        // Verify values change over time as expected
        Assert.True(result[_nowUtc.AddDays(-10)] >= 1000m);
        Assert.True(result[_nowUtc.AddDays(-7)] >= 1500m);
        Assert.True(result[_nowUtc.AddDays(-5)] >= 1200m);
        Assert.True(result[_nowUtc.AddDays(-2)] >= 1400m);
        Assert.True(result[_nowUtc] >= 1400m);
    }

    [Fact]
    public async Task GetNetWorth_EmptyPortfolio_ReturnsZero()
    {
        // Arrange - Create user with no accounts
        Authorize("EmptyUser", 999, UserRole.User);

        // Act
        var result = await Client.GetFromJsonAsync<decimal?>($"api/MoneyFlow/GetNetWorth/999/{DefaultCurrency.USD.Id}/{_nowUtc:O}", TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(0m, result);
    }

    [Fact]
    public async Task GetNetWorth_BoundaryDates_ReturnsCorrectValues()
    {
        // Arrange

        var bondFirstDate = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        _testDatabase.Context.CurrencyEntries.Add(
            new CurrencyAccountEntry(null, 1, bondFirstDate, 1000m, 1000m) { UserId = 1, CurrencyId = DefaultCurrency.PLN.Id, Labels = [] });
        _testDatabase.Context.CurrencyEntries.Add(
            new CurrencyAccountEntry(null, 2, bondFirstDate.AddYears(1), 1500m, 500m) { UserId = 1, CurrencyId = DefaultCurrency.PLN.Id, Labels = [] });

        await _testDatabase.Context.SaveChangesAsync(TestContext.Current.CancellationToken);

        Authorize("TestUser", 1, UserRole.User);

        // Act - Test at earliest entry date
        var firstDate = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var resultAtFirst = await Client.GetFromJsonAsync<decimal?>($"api/MoneyFlow/GetNetWorth/1/{DefaultCurrency.USD.Id}/{firstDate:O}", TestContext.Current.CancellationToken);

        // Act - Test date between entries
        var middleDate = firstDate.AddMonths(6);
        var resultAtMiddle = await Client.GetFromJsonAsync<decimal?>($"api/MoneyFlow/GetNetWorth/1/{DefaultCurrency.USD.Id}/{middleDate:O}", TestContext.Current.CancellationToken);

        // Act - Test at latest entry date
        var lastDate = firstDate.AddYears(1);
        var resultAtLast = await Client.GetFromJsonAsync<decimal?>($"api/MoneyFlow/GetNetWorth/1/{DefaultCurrency.USD.Id}/{lastDate:O}", TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(resultAtFirst);
        Assert.True(resultAtFirst >= 1000m, $"At first date, expected >= 1000, got {resultAtFirst}");

        Assert.NotNull(resultAtMiddle);
        Assert.True(resultAtMiddle >= 1000m, $"At middle date, expected >= 1000, got {resultAtMiddle}");

        Assert.NotNull(resultAtLast);
        Assert.True(resultAtLast >= 1500m, $"At last date, expected >= 1500, got {resultAtLast}");
    }

    [Fact]
    public async Task GetNetWorth_RangeVersion_MatchesSingleDateResults()
    {
        // Arrange - Currency + Bond + Stock accounts spanning multiple dates
        await SeedCashEntries();

        _testDatabase.Context.Bonds.Add(CreateBondDetails(30, _nowUtc));
        await _testDatabase.Context.SaveChangesAsync(TestContext.Current.CancellationToken);

        _testDatabase.Context.BondEntries.Add(new BondAccountEntry(null, 1, _nowUtc.AddDays(-9), 1000m, 1000m, 30) { UserId = 1 });
        _testDatabase.Context.BondEntries.Add(new BondAccountEntry(null, 2, _nowUtc.AddDays(-4), 1500m, 500m, 30) { UserId = 1 });
        await _testDatabase.Context.SaveChangesAsync(TestContext.Current.CancellationToken);

        Authorize("TestUser", 1, UserRole.User);

        var client = new MoneyFlowHttpClient(Client);
        var rangeStart = _nowUtc.AddDays(-7);
        var rangeEnd = _nowUtc;

        // Act - call range endpoint once and compare against per-day single-date calls
        var rangeResult = await client.GetNetWorth(1, DefaultCurrency.USD, rangeStart, rangeEnd);

        var expected = new Dictionary<DateTime, decimal?>();
        for (DateTime date = rangeEnd; date >= rangeStart; date = date.AddDays(-1))
            expected[date] = await Client.GetFromJsonAsync<decimal?>($"api/MoneyFlow/GetNetWorth/1/{DefaultCurrency.USD.Id}/{date:O}", TestContext.Current.CancellationToken);

        // Assert - same days, same values
        Assert.NotNull(rangeResult);
        Assert.Equal(expected.Count, rangeResult.Count);
        foreach (var (date, singleValue) in expected)
        {
            Assert.True(rangeResult.ContainsKey(date), $"Range result missing date {date:o}");
            Assert.True(singleValue.HasValue, $"Single-date result missing value for {date:o}");
            Assert.Equal(singleValue!.Value, rangeResult[date]);
        }
    }

    [Fact]
    public async Task GetNetWorth_RangeVersion_PerformsEfficiently()
    {
        // Arrange - 500 entries over 500 days (mirrors GetNetWorth_LargePortfolio_PerformsEfficiently)

        for (int i = 0; i < 500; i++)
        {
            _testDatabase.Context.CurrencyEntries.Add(
                new CurrencyAccountEntry(null, i + 1, _nowUtc.AddDays(-500 + i), 0, 100m) { UserId = 1, CurrencyId = DefaultCurrency.PLN.Id, Labels = [] });
        }
        await _testDatabase.Context.SaveChangesAsync(TestContext.Current.CancellationToken);

        Authorize("TestUser", 1, UserRole.User);
        var client = new MoneyFlowHttpClient(Client);

        // Warm up first, for the same reason as GetNetWorth_LargePortfolio_PerformsEfficiently.
        await client.GetNetWorth(1, DefaultCurrency.USD, _nowUtc.AddDays(-365), _nowUtc);

        // Act - 365-day range
        var startTime = DateTime.UtcNow;
        var result = await client.GetNetWorth(1, DefaultCurrency.USD, _nowUtc.AddDays(-365), _nowUtc);
        var duration = DateTime.UtcNow - startTime;

        // Assert
        Assert.NotNull(result);
        Assert.NotEmpty(result);
        Assert.True(duration.TotalSeconds < 5,
            $"GetNetWorth range took {duration.TotalSeconds}s, expected < 5s");
    }

    [Fact]
    public async Task GetNetWorth_MultipleUsersIsolation_ReturnsOnlyUserData()
    {
        // Arrange - Create accounts for two different users

        _testDatabase.Context.CurrencyEntries.Add(new CurrencyAccountEntry(null, 1, _nowUtc, 1000m, 1000m) { UserId = 1, CurrencyId = DefaultCurrency.PLN.Id, Labels = [] });
        _testDatabase.Context.CurrencyEntries.Add(new CurrencyAccountEntry(null, 2, _nowUtc, 5000m, 5000m) { UserId = 2, CurrencyId = DefaultCurrency.PLN.Id, Labels = [] });
        await _testDatabase.Context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act - User 1 gets their net worth
        Authorize("User1", 1, UserRole.User);
        var user1Result = await Client.GetFromJsonAsync<decimal?>($"api/MoneyFlow/GetNetWorth/1/{DefaultCurrency.USD.Id}/{_nowUtc:O}", TestContext.Current.CancellationToken);

        // Act - User 2 gets their net worth
        Authorize("User2", 2, UserRole.User);
        var user2Result = await Client.GetFromJsonAsync<decimal?>($"api/MoneyFlow/GetNetWorth/2/{DefaultCurrency.USD.Id}/{_nowUtc:O}", TestContext.Current.CancellationToken);

        // Assert - Each user should only see their own data
        Assert.NotNull(user1Result);
        Assert.NotNull(user2Result);

        // user1Result should include account 20 (1000) but not account 21
        // user2Result should include account 21 (5000) but not account 20
        Assert.NotEqual(user1Result, user2Result);
    }

    public static TheoryData<Func<DateTime, string>> OtherUserMoneyFlowEndpointUrls => new()
    {
        now => $"api/MoneyFlow/GetNetWorth/2/{DefaultCurrency.USD.Id}/{now:O}",
        now => $"api/MoneyFlow/GetNetWorth/2/{DefaultCurrency.USD.Id}/{now.AddDays(-7):O}/{now:O}",
        now => $"api/MoneyFlow/GetInflow/2/{DefaultCurrency.USD.Id}/{now.AddDays(-7):O}/{now:O}",
        now => $"api/MoneyFlow/GetOutflow/2/{DefaultCurrency.USD.Id}/{now.AddDays(-7):O}/{now:O}",
        now => $"api/MoneyFlow/GetNetCashFlow/2/{DefaultCurrency.USD.Id}/{now.AddDays(-7):O}/{now:O}",
        now => $"api/MoneyFlow/GetCapital/2/{DefaultCurrency.USD.Id}/{now.AddDays(-7):O}/{now:O}",
        now => $"api/MoneyFlow/GetClosingBalance/2/{DefaultCurrency.USD.Id}/{now.AddDays(-7):O}/{now:O}",
        now => $"api/MoneyFlow/GetLabelsValue?userId=2&start={now.AddDays(-7):O}&end={now:O}",
        now => $"api/MoneyFlow/GetInvestmentRate?userId=2&currencyId={DefaultCurrency.USD.Id}&start={now.AddDays(-7):O}&end={now:O}",
        now => $"api/MoneyFlow/GetSavingsRate?userId=2&currencyId={DefaultCurrency.PLN.Id}&start={now.AddDays(-7):O}&end={now:O}",
    };

    [Theory]
    [MemberData(nameof(OtherUserMoneyFlowEndpointUrls))]
    public async Task UserScopedEndpoints_ForOtherUser_ReturnForbidden(Func<DateTime, string> endpointUrl)
    {
        Authorize("User1", 1, UserRole.User);

        var response = await Client.GetAsync(endpointUrl(_nowUtc), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // Seeds a cash account that books a -500 "Investment" outflow alongside an investment account
    // (Stock-type, new asset model) that holds the matching shares. The holdings value belongs in
    // closing balance, the cash outflow in cash flow — and neither must leak into the other.
    private async Task SeedCashAndInvestmentAccounts(DateTime day0, bool includeHighPriceSell = false)
    {
        // Cash: +1000 on day0, then the −500 "Investment" outflow on day0+1.
        _testDatabase.Context.CurrencyEntries.Add(new CurrencyAccountEntry(null, 1, day0, 1000m, 1000m) { UserId = 1, CurrencyId = DefaultCurrency.PLN.Id, Labels = [] });
        _testDatabase.Context.CurrencyEntries.Add(new CurrencyAccountEntry(null, 2, day0.AddDays(1), 500m, -500m) { UserId = 1, CurrencyId = DefaultCurrency.PLN.Id, Labels = [] });

        // Investment listing priced at 100 USD with quotes across the whole window.
        const long listingId = 9100;
        _testDatabase.Context.AssetListings.Add(new AssetListing
        {
            Id = listingId,
            AssetId = 1,
            Ticker = "CSPX",
            ExchangeMic = "XNAS",
            ExchangeName = "United States",
            TradingCurrency = "USD",
            PriceMultiplier = 1m,
            IsActive = true
        });
        for (int i = 0; i <= 2; i++)
            _testDatabase.Context.PriceQuotes.Add(new PriceQuote
            {
                AssetListingId = listingId,
                Provider = MarketDataProvider.AlphaVantage,
                Price = 100m,
                Currency = "USD",
                PriceTime = new DateTimeOffset(day0.AddDays(i), TimeSpan.Zero),
                QuoteType = PriceQuoteType.EndOfDay,
                FetchedAt = DateTimeOffset.UtcNow
            });

        // Buy 5 units on day0+1 (mirrors the cash outflow), so holdings value = 5 * 100 = 500 USD.
        _testDatabase.Context.InvestmentTransactions.Add(new InvestmentTransaction
        {
            UserId = 1,

            AssetListingId = listingId,
            Type = InvestmentTransactionType.Buy,
            Quantity = 5m,
            UnitPrice = 100m,
            Currency = "USD",
            TradeDate = DateOnly.FromDateTime(day0.AddDays(1))
        });

        if (includeHighPriceSell)
        {
            _testDatabase.Context.InvestmentTransactions.Add(new InvestmentTransaction
            {
                UserId = 1,

                AssetListingId = listingId,
                Type = InvestmentTransactionType.Sell,
                Quantity = 2m,
                UnitPrice = 250m,
                Currency = "USD",
                TradeDate = DateOnly.FromDateTime(day0.AddDays(2))
            });
        }

        await _testDatabase.Context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task GetClosingBalance_IncludesInvestmentAccountHoldings()
    {
        var day0 = new DateTime(2024, 6, 1, 0, 0, 0, DateTimeKind.Utc);
        await SeedCashAndInvestmentAccounts(day0);
        Authorize("TestUser", 1, UserRole.User);

        var result = await new MoneyFlowHttpClient(Client).GetClosingBalance(1, DefaultCurrency.USD, day0, day0.AddDays(2));

        // day0: cash 1000, no holdings yet → 1000. day0+1: cash drops to 500 but holdings add 500 → 1000.
        Assert.Equal(1000m, result.Single(x => x.DateTime == day0).Value);
        Assert.Equal(1000m, result.Single(x => x.DateTime == day0.AddDays(1)).Value);
        Assert.Equal(1000m, result.Single(x => x.DateTime == day0.AddDays(2)).Value);
    }

    [Fact]
    public async Task NetWorthAndBalances_PoolAccountLinkedAndUnlinkedEntries_PerOwner()
    {
        var day0 = new DateTime(2024, 6, 1, 0, 0, 0, DateTimeKind.Utc);
        _testDatabase.Context.CurrencyEntries.AddRange(
            new CurrencyAccountEntry(null, 1, day0, 0, 1000m) { UserId = 1, CurrencyId = DefaultCurrency.PLN.Id, Labels = [] },
            // Entries without an account still belong to their owner; another owner's entry never counts.
            new CurrencyAccountEntry(null, 2, day0.AddDays(1), 0, 250m) { Labels = [], UserId = 1, CurrencyId = 0 },
            new CurrencyAccountEntry(null, 3, day0.AddDays(1), 0, 9999m) { Labels = [], UserId = 2, CurrencyId = 0 });
        await _testDatabase.Context.SaveChangesAsync(TestContext.Current.CancellationToken);
        Authorize("TestUser", 1, UserRole.User);
        var client = new MoneyFlowHttpClient(Client);

        var netWorth = await client.GetNetWorth(1, DefaultCurrency.USD, day0, day0.AddDays(2));
        var closing = await client.GetClosingBalance(1, DefaultCurrency.USD, day0, day0.AddDays(2));
        var net = await client.GetNetCashFlow(1, DefaultCurrency.USD, day0, day0.AddDays(2));

        Assert.Equal([1000m, 1250m, 1250m], netWorth.OrderBy(x => x.Key).Select(x => x.Value));
        Assert.Equal([1000m, 1250m, 1250m], closing.OrderBy(x => x.DateTime).Select(x => x.Value));
        Assert.Equal(1250m, net.Sum(x => x.Value));
    }

    [Fact]
    public async Task CashFlow_DoesNotDoubleCountInvestmentHoldings()
    {
        var day0 = new DateTime(2024, 6, 1, 0, 0, 0, DateTimeKind.Utc);
        await SeedCashAndInvestmentAccounts(day0);
        Authorize("TestUser", 1, UserRole.User);

        var client = new MoneyFlowHttpClient(Client);
        var netCashFlow = await client.GetNetCashFlow(1, DefaultCurrency.USD, day0, day0.AddDays(2));
        var inflow = await client.GetInflow(1, DefaultCurrency.USD, day0, day0.AddDays(2));
        var outflow = await client.GetOutflow(1, DefaultCurrency.USD, day0, day0.AddDays(2));

        // Only the cash account contributes: +1000 in, −500 out, net +500. The 500 of investment
        // holdings value must NOT surface as an inflow, or money-flow double counts the cash outflow.
        Assert.Equal(500m, netCashFlow.Sum(x => x.Value));
        Assert.Equal(1000m, inflow.Sum(x => x.Value));
        Assert.Equal(-500m, outflow.Sum(x => x.Value));
        Assert.Equal(-500m, netCashFlow.Single(x => x.DateTime == day0.AddDays(1)).Value);
    }

    [Fact]
    public async Task GetCapital_IncludesInvestmentAccountTrades()
    {
        var day0 = new DateTime(2024, 6, 1, 0, 0, 0, DateTimeKind.Utc);
        await SeedCashAndInvestmentAccounts(day0);
        Authorize("TestUser", 1, UserRole.User);

        var result = await new MoneyFlowHttpClient(Client).GetCapital(
            1,
            DefaultCurrency.USD,
            day0,
            day0.AddDays(2));

        Assert.Equal(0m, result.Single(x => x.DateTime == day0).Value);
        Assert.Equal(500m, result.Single(x => x.DateTime == day0.AddDays(1)).Value);
        Assert.Equal(500m, result.Single(x => x.DateTime == day0.AddDays(2)).Value);
    }

    [Fact]
    public async Task GetCapital_ReconstructsSellFromHistoricalCost_NotSaleProceeds()
    {
        var day0 = new DateTime(2024, 6, 1, 0, 0, 0, DateTimeKind.Utc);
        await SeedCashAndInvestmentAccounts(day0, includeHighPriceSell: true);
        Authorize("TestUser", 1, UserRole.User);

        var result = await new MoneyFlowHttpClient(Client).GetCapital(
            1,
            DefaultCurrency.USD,
            day0,
            day0.AddDays(2));

        Assert.Equal(0m, result.Single(x => x.DateTime == day0).Value);
        Assert.Equal(500m, result.Single(x => x.DateTime == day0.AddDays(1)).Value);
        // The sell is for 2 of the 5 units, so only 2 * 100 of historical cost leaves capital;
        // the 250 sale price must not create a discontinuity in the Capital series.
        Assert.Equal(300m, result.Single(x => x.DateTime == day0.AddDays(2)).Value);
    }

    public override void Dispose()
    {
        base.Dispose();
        if (_testDatabase is null)
            return;

        _testDatabase.Dispose();
        _testDatabase = null!;
        GC.SuppressFinalize(this);
    }
}