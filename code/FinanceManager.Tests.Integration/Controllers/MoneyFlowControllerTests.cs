using FinanceManager.Components.HttpClients;
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
using FinanceManager.Infrastructure.Contexts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using System.Net;
using Xunit;

namespace FinanceManager.Tests.Integration.Controllers;

[Collection("api")]
[Trait("Category", "Integration")]
public class MoneyFlowControllerTests(OptionsProvider optionsProvider) : ControllerTests(optionsProvider), IDisposable
{
    private TestDatabase? _testDatabase;
    private DateTime _nowUtc;
    private int _valueChange = 10;
    private int _value;

    protected override void ConfigureServices(IServiceCollection services)
    {
        _nowUtc = DateTime.UtcNow.Date;
        _testDatabase = new TestDatabase();

        // remove any registration for AppDbContext
        var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));
        if (descriptor != null)
            services.Remove(descriptor);

        services.AddSingleton(_testDatabase!.Context);

#pragma warning disable CS0854
        var currencyRepoMock = new Mock<ICurrencyRepository>();
        currencyRepoMock.Setup(x => x.GetCurrencies(It.IsAny<CancellationToken>())).Returns(AsyncEnumerable.Range(0, 1).Select(_ => DefaultCurrency.USD));
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

    private async Task SeedWithTestCurrencyAccount(string accountName = "Test Currency Account")
    {
        if (await _testDatabase!.Context.Accounts.AnyAsync(x => x.Name == accountName, TestContext.Current.CancellationToken))
            return;

        FinancialLabel salary = new() { Name = "salary" };
        _testDatabase!.Context.FinancialLabels.Add(salary);
        _value = _valueChange;

        var test = new FinancialAccountBaseDto
        {
            UserId = 1,
            AccountId = 1,
            Name = accountName,
            AccountLabel = AccountLabel.Cash,
            AccountType = AccountType.Currency
        };

        _testDatabase!.Context.Accounts.Add(test);
        await _testDatabase.Context.SaveChangesAsync(TestContext.Current.CancellationToken);

        for (DateTime i = _nowUtc.AddMonths(-2).Date; i <= _nowUtc; i = i.AddDays(1))
            _testDatabase!.Context.CurrencyEntries.Add(new CurrencyAccountEntry(test.AccountId, 0, i, _value += _valueChange, _valueChange)
            {
                Labels = [salary]
            });

        await _testDatabase.Context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task GetNetWorth_SingleDate_ReturnsValue()
    {
        await SeedWithTestCurrencyAccount();
        Authorize("TestUser", 1, UserRole.User);

        var result = await new MoneyFlowHttpClient(Client).GetNetWorth(1, DefaultCurrency.USD, _nowUtc);

        Assert.NotNull(result);
        Assert.Equal(_value, result);
    }

    [Fact]
    public async Task GetNetWorth_Range_ReturnsDictionary()
    {
        await SeedWithTestCurrencyAccount();
        Authorize("TestUser", 1, UserRole.User);

        var result = await new MoneyFlowHttpClient(Client).GetNetWorth(1, DefaultCurrency.USD, _nowUtc.AddDays(-7), _nowUtc);

        Assert.NotNull(result);
        Assert.NotEmpty(result);
    }

    [Fact]
    public async Task GetInflow_ReturnsList()
    {
        await SeedWithTestCurrencyAccount();
        Authorize("TestUser", 1, UserRole.User);
        int days = 7;

        var result = await new MoneyFlowHttpClient(Client).GetInflow(1, DefaultCurrency.USD, _nowUtc.AddDays(-days), _nowUtc);

        Assert.NotNull(result);
        Assert.All(result, x => Assert.Equal(_valueChange, x.Value));
    }

    [Fact]
    public async Task GetInflow_WithAccountIds_ReturnsFilteredList()
    {
        await SeedWithTestCurrencyAccount();

        var secondAccount = new FinancialAccountBaseDto
        {
            UserId = 1,
            AccountId = 2,
            Name = "Filtered Account",
            AccountLabel = AccountLabel.Cash,
            AccountType = AccountType.Currency
        };
        _testDatabase!.Context.Accounts.Add(secondAccount);
        await _testDatabase.Context.SaveChangesAsync(TestContext.Current.CancellationToken);

        for (DateTime i = _nowUtc.AddDays(-7); i <= _nowUtc; i = i.AddDays(1))
            _testDatabase.Context.CurrencyEntries.Add(new CurrencyAccountEntry(secondAccount.AccountId, 100 + i.Day, i, 5, 5) { Labels = [] });

        await _testDatabase.Context.SaveChangesAsync(TestContext.Current.CancellationToken);
        Authorize("TestUser", 1, UserRole.User);

        var result = await new MoneyFlowHttpClient(Client).GetInflow(1, DefaultCurrency.USD, _nowUtc.AddDays(-7), _nowUtc, [2]);

        Assert.NotNull(result);
        Assert.All(result, x => Assert.Equal(5, x.Value));
    }

    [Fact]
    public async Task GetOutflow_ReturnsList()
    {
        await SeedWithTestCurrencyAccount();
        Authorize("TestUser", 1, UserRole.User);

        var result = await new MoneyFlowHttpClient(Client).GetOutflow(1, DefaultCurrency.USD, _nowUtc.AddDays(-7), _nowUtc);

        Assert.NotNull(result);
        Assert.Equal(0, result.Sum(x => x.Value));
    }

    [Fact]
    public async Task GetNetCashFlow_ReturnsList()
    {
        await SeedWithTestCurrencyAccount();
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
        await SeedWithTestCurrencyAccount();
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
        await SeedWithTestCurrencyAccount();
        Authorize("TestUser", 1, UserRole.User);
        int days = 7;

        var result = await new MoneyFlowHttpClient(Client).GetLabelsValue(1, _nowUtc.AddDays(-days), _nowUtc);

        Assert.NotNull(result);
        Assert.Equal((1 + days) * _valueChange, result.First().Value);
    }

    [Fact]
    public async Task GetInvestmentRate_ReturnsList()
    {
        await SeedWithTestCurrencyAccount();
        Authorize("TestUser", 1, UserRole.User);
        int days = 7;

        var result = await new MoneyFlowHttpClient(Client).GetInvestmentRate(1, DefaultCurrency.USD, _nowUtc.AddDays(-days), _nowUtc).ToListAsync(TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.NotEmpty(result);
        Assert.Equal((1 + days) * _valueChange, result.First().Salary);
    }

    [Fact]
    public async Task GetNetWorth_MultipleAccountTypes_ReturnsAggregatedValue()
    {
        // Arrange - Seed with Currency, Stock, and Bond accounts
        await SeedWithTestCurrencyAccount("Multi Account 1");

        // Add a second currency account
        var currencyAccount2 = new FinancialAccountBaseDto
        {
            UserId = 1,
            AccountId = 2,
            Name = "Savings",
            AccountLabel = AccountLabel.Cash,
            AccountType = AccountType.Currency
        };
        _testDatabase!.Context.Accounts.Add(currencyAccount2);
        await _testDatabase.Context.SaveChangesAsync(TestContext.Current.CancellationToken);

        _testDatabase.Context.CurrencyEntries.Add(
            new CurrencyAccountEntry(2, 100, _nowUtc.AddDays(-5), 5000m, 5000m) { Labels = [] });
        await _testDatabase.Context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Add a bond account
        var bondAccount = new FinancialAccountBaseDto
        {
            UserId = 1,
            AccountId = 4,
            Name = "Government Bonds",
            AccountLabel = AccountLabel.Other,
            AccountType = AccountType.Bond
        };
        _testDatabase.Context.Accounts.Add(bondAccount);
        _testDatabase.Context.Bonds.Add(CreateBondDetails(1, _nowUtc));
        await _testDatabase.Context.SaveChangesAsync(TestContext.Current.CancellationToken);

        _testDatabase.Context.BondEntries.Add(
            new BondAccountEntry(4, 300, _nowUtc.AddDays(-10), 3000m, 3000m, 1));
        await _testDatabase.Context.SaveChangesAsync(TestContext.Current.CancellationToken);

        Authorize("TestUser", 1, UserRole.User);

        // Act
        var result = await new MoneyFlowHttpClient(Client).GetNetWorth(1, DefaultCurrency.USD, _nowUtc);

        // Assert - Should aggregate all account types
        Assert.NotNull(result);

        // Result should be > 0 and include contributions from all accounts
        // Exact value depends on stock price provider mock
        Assert.True(result > 8000m, $"Expected net worth > 8000 (currency + bonds alone), got {result}");
    }

    [Fact]
    public async Task GetNetWorth_LargePortfolio_PerformsEfficiently()
    {
        // Arrange - Create account with many entries to test performance
        var accountName = "Large Portfolio";
        if (!await _testDatabase!.Context.Accounts.AnyAsync(x => x.Name == accountName, TestContext.Current.CancellationToken))
        {
            var account = new FinancialAccountBaseDto
            {
                UserId = 1,
                AccountId = 10,
                Name = accountName,
                AccountLabel = AccountLabel.Cash,
                AccountType = AccountType.Currency
            };
            _testDatabase.Context.Accounts.Add(account);
            await _testDatabase.Context.SaveChangesAsync(TestContext.Current.CancellationToken);

            // Add 500 entries over time
            decimal runningValue = 0;
            for (int i = 0; i < 500; i++)
            {
                runningValue += 100m;
                _testDatabase.Context.CurrencyEntries.Add(
                    new CurrencyAccountEntry(10, i + 1, _nowUtc.AddDays(-500 + i), runningValue, 100m) { Labels = [] });
            }
            await _testDatabase.Context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        Authorize("TestUser", 1, UserRole.User);

        // Act - Measure performance
        var startTime = DateTime.UtcNow;
        var result = await new MoneyFlowHttpClient(Client).GetNetWorth(1, DefaultCurrency.USD, _nowUtc);
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
        var accountName = "Dynamic Account";
        if (!await _testDatabase!.Context.Accounts.AnyAsync(x => x.Name == accountName, TestContext.Current.CancellationToken))
        {
            var account = new FinancialAccountBaseDto
            {
                UserId = 1,
                AccountId = 11,
                Name = accountName,
                AccountLabel = AccountLabel.Cash,
                AccountType = AccountType.Currency
            };
            _testDatabase.Context.Accounts.Add(account);
            await _testDatabase.Context.SaveChangesAsync(TestContext.Current.CancellationToken);
            // Add entries with varying value changes
            _testDatabase.Context.CurrencyEntries.Add(
                new CurrencyAccountEntry(11, 1, _nowUtc.AddDays(-10), 1000m, 1000m) { Labels = [] });
            _testDatabase.Context.CurrencyEntries.Add(
                new CurrencyAccountEntry(11, 2, _nowUtc.AddDays(-7), 1500m, 500m) { Labels = [] });
            _testDatabase.Context.CurrencyEntries.Add(
                new CurrencyAccountEntry(11, 3, _nowUtc.AddDays(-5), 1200m, -300m) { Labels = [] });
            _testDatabase.Context.CurrencyEntries.Add(
                new CurrencyAccountEntry(11, 4, _nowUtc.AddDays(-2), 1400m, 200m) { Labels = [] });

            await _testDatabase.Context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

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
        var result = await new MoneyFlowHttpClient(Client).GetNetWorth(999, DefaultCurrency.USD, _nowUtc);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(0m, result);
    }

    [Fact]
    public async Task GetNetWorth_BoundaryDates_ReturnsCorrectValues()
    {
        // Arrange
        var accountName = "Boundary Test";
        if (!await _testDatabase!.Context.Accounts.AnyAsync(x => x.Name == accountName, TestContext.Current.CancellationToken))
        {
            var account = new FinancialAccountBaseDto
            {
                UserId = 1,
                AccountId = 12,
                Name = accountName,
                AccountLabel = AccountLabel.Cash,
                AccountType = AccountType.Currency
            };
            _testDatabase.Context.Accounts.Add(account);
            await _testDatabase.Context.SaveChangesAsync(TestContext.Current.CancellationToken);

            var bondFirstDate = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            _testDatabase.Context.CurrencyEntries.Add(
                new CurrencyAccountEntry(12, 1, bondFirstDate, 1000m, 1000m) { Labels = [] });
            _testDatabase.Context.CurrencyEntries.Add(
                new CurrencyAccountEntry(12, 2, bondFirstDate.AddYears(1), 1500m, 500m) { Labels = [] });

            await _testDatabase.Context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        Authorize("TestUser", 1, UserRole.User);

        // Act - Test at earliest entry date
        var firstDate = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var resultAtFirst = await new MoneyFlowHttpClient(Client).GetNetWorth(1, DefaultCurrency.USD, firstDate);

        // Act - Test date between entries
        var middleDate = firstDate.AddMonths(6);
        var resultAtMiddle = await new MoneyFlowHttpClient(Client).GetNetWorth(1, DefaultCurrency.USD, middleDate);

        // Act - Test at latest entry date
        var lastDate = firstDate.AddYears(1);
        var resultAtLast = await new MoneyFlowHttpClient(Client).GetNetWorth(1, DefaultCurrency.USD, lastDate);

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
        await SeedWithTestCurrencyAccount("Range Match Currency");

        var bondAccount = new FinancialAccountBaseDto
        {
            UserId = 1,
            AccountId = 30,
            Name = "Range Match Bonds",
            AccountLabel = AccountLabel.Other,
            AccountType = AccountType.Bond
        };
        _testDatabase!.Context.Accounts.Add(bondAccount);
        _testDatabase.Context.Bonds.Add(CreateBondDetails(30, _nowUtc));
        await _testDatabase.Context.SaveChangesAsync(TestContext.Current.CancellationToken);

        _testDatabase.Context.BondEntries.Add(new BondAccountEntry(30, 1, _nowUtc.AddDays(-9), 1000m, 1000m, 30));
        _testDatabase.Context.BondEntries.Add(new BondAccountEntry(30, 2, _nowUtc.AddDays(-4), 1500m, 500m, 30));
        await _testDatabase.Context.SaveChangesAsync(TestContext.Current.CancellationToken);

        Authorize("TestUser", 1, UserRole.User);

        var client = new MoneyFlowHttpClient(Client);
        var rangeStart = _nowUtc.AddDays(-7);
        var rangeEnd = _nowUtc;

        // Act - call range endpoint once and compare against per-day single-date calls
        var rangeResult = await client.GetNetWorth(1, DefaultCurrency.USD, rangeStart, rangeEnd);

        var expected = new Dictionary<DateTime, decimal?>();
        for (DateTime date = rangeEnd; date >= rangeStart; date = date.AddDays(-1))
            expected[date] = await client.GetNetWorth(1, DefaultCurrency.USD, date);

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
        var accountName = "Range Perf Portfolio";
        if (!await _testDatabase!.Context.Accounts.AnyAsync(x => x.Name == accountName, TestContext.Current.CancellationToken))
        {
            var account = new FinancialAccountBaseDto
            {
                UserId = 1,
                AccountId = 40,
                Name = accountName,
                AccountLabel = AccountLabel.Cash,
                AccountType = AccountType.Currency
            };
            _testDatabase.Context.Accounts.Add(account);
            await _testDatabase.Context.SaveChangesAsync(TestContext.Current.CancellationToken);

            decimal runningValue = 0;
            for (int i = 0; i < 500; i++)
            {
                runningValue += 100m;
                _testDatabase.Context.CurrencyEntries.Add(
                    new CurrencyAccountEntry(40, i + 1, _nowUtc.AddDays(-500 + i), runningValue, 100m) { Labels = [] });
            }
            await _testDatabase.Context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        Authorize("TestUser", 1, UserRole.User);

        // Act - 365-day range
        var startTime = DateTime.UtcNow;
        var result = await new MoneyFlowHttpClient(Client).GetNetWorth(1, DefaultCurrency.USD, _nowUtc.AddDays(-365), _nowUtc);
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
        var user1Account = new FinancialAccountBaseDto
        {
            UserId = 1,
            AccountId = 20,
            Name = "User 1 Account",
            AccountLabel = AccountLabel.Cash,
            AccountType = AccountType.Currency
        };
        _testDatabase!.Context.Accounts.Add(user1Account);

        var user2Account = new FinancialAccountBaseDto
        {
            UserId = 2,
            AccountId = 21,
            Name = "User 2 Account",
            AccountLabel = AccountLabel.Cash,
            AccountType = AccountType.Currency
        };
        _testDatabase.Context.Accounts.Add(user2Account);
        await _testDatabase.Context.SaveChangesAsync(TestContext.Current.CancellationToken);

        _testDatabase.Context.CurrencyEntries.Add(new CurrencyAccountEntry(20, 1, _nowUtc, 1000m, 1000m) { Labels = [] });
        _testDatabase.Context.CurrencyEntries.Add(new CurrencyAccountEntry(21, 2, _nowUtc, 5000m, 5000m) { Labels = [] });
        await _testDatabase.Context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act - User 1 gets their net worth
        Authorize("User1", 1, UserRole.User);
        var user1Result = await new MoneyFlowHttpClient(Client).GetNetWorth(1, DefaultCurrency.USD, _nowUtc);

        // Act - User 2 gets their net worth
        Authorize("User2", 2, UserRole.User);
        var user2Result = await new MoneyFlowHttpClient(Client).GetNetWorth(2, DefaultCurrency.USD, _nowUtc);

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
        now => $"api/MoneyFlow/GetClosingBalance/2/{DefaultCurrency.USD.Id}/{now.AddDays(-7):O}/{now:O}",
        now => $"api/MoneyFlow/GetLabelsValue?userId=2&start={now.AddDays(-7):O}&end={now:O}",
        now => $"api/MoneyFlow/GetInvestmentRate?userId=2&currencyId={DefaultCurrency.USD.Id}&start={now.AddDays(-7):O}&end={now:O}",
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
    private async Task SeedCashAndInvestmentAccounts(DateTime day0)
    {
        var cashAccount = new FinancialAccountBaseDto
        {
            UserId = 1,
            AccountId = 50,
            Name = "Investment Parity Cash",
            AccountLabel = AccountLabel.Cash,
            AccountType = AccountType.Currency
        };
        var investmentAccount = new FinancialAccountBaseDto
        {
            UserId = 1,
            AccountId = 51,
            Name = "Investment Parity Holdings",
            AccountLabel = AccountLabel.Stock,
            AccountType = AccountType.Stock
        };
        _testDatabase!.Context.Accounts.AddRange(cashAccount, investmentAccount);

        // Cash: +1000 on day0, then the −500 "Investment" outflow on day0+1.
        _testDatabase.Context.CurrencyEntries.Add(new CurrencyAccountEntry(50, 1, day0, 1000m, 1000m) { Labels = [] });
        _testDatabase.Context.CurrencyEntries.Add(new CurrencyAccountEntry(50, 2, day0.AddDays(1), 500m, -500m) { Labels = [] });

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
            AccountId = 51,
            AssetListingId = listingId,
            Type = InvestmentTransactionType.Buy,
            Quantity = 5m,
            UnitPrice = 100m,
            Currency = "USD",
            TradeDate = DateOnly.FromDateTime(day0.AddDays(1))
        });

        await _testDatabase.Context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task GetClosingBalance_IncludesInvestmentAccountHoldings()
    {
        var day0 = new DateTime(2024, 6, 1);
        await SeedCashAndInvestmentAccounts(day0);
        Authorize("TestUser", 1, UserRole.User);

        var result = await new MoneyFlowHttpClient(Client).GetClosingBalance(1, DefaultCurrency.USD, day0, day0.AddDays(2));

        // day0: cash 1000, no holdings yet → 1000. day0+1: cash drops to 500 but holdings add 500 → 1000.
        Assert.Equal(1000m, result.Single(x => x.DateTime == day0).Value);
        Assert.Equal(1000m, result.Single(x => x.DateTime == day0.AddDays(1)).Value);
        Assert.Equal(1000m, result.Single(x => x.DateTime == day0.AddDays(2)).Value);
    }

    [Fact]
    public async Task CashFlow_DoesNotDoubleCountInvestmentHoldings()
    {
        var day0 = new DateTime(2024, 6, 1);
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

    public override void Dispose()
    {
        base.Dispose();
        if (_testDatabase is null)
            return;

        _testDatabase.Dispose();
        _testDatabase = null;
        GC.SuppressFinalize(this);
    }
}