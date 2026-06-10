using FinanceManager.Application.FinancialAccounts.Stock.Seeders;
using FinanceManager.Domain.Entities.Currencies;
using FinanceManager.Domain.Entities.Stocks;
using FinanceManager.Domain.Repositories;
using FinanceManager.Domain.Repositories.Account;
using FinanceManager.Domain.ValueObjects;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace FinanceManager.Tests.Unit.Application.Services.Seeders;

[Collection("Application")]
[Trait("Category", "Unit")]
public class StockAccountSeederTests
{
    // Mirrors the cash seeder's recurring "Investment" outflow that the stock buys are synced to.
    private const decimal _monthlyInvestment = 500m;
    private const int _investmentDay = 3;

    private static readonly Currency _usd = new(1, "USD", "$");

    private readonly Mock<IFinancialAccountRepository> _accountRepository = new();
    private readonly Mock<IAccountRepository<StockAccount>> _stockAccountRepository = new();
    private readonly Mock<IStockPriceRepository> _stockPriceRepository = new();
    private readonly Mock<IStockDetailsRepository> _stockDetailsRepository = new();
    private readonly Mock<ICurrencyRepository> _currencyRepository = new();
    private readonly Dictionary<DateTime, decimal> _seededPrices = [];
    private readonly List<StockAccount> _savedAccounts = [];
    private readonly StockAccountSeeder _seeder;

    public StockAccountSeederTests()
    {
        _stockAccountRepository
            .Setup(r => r.GetAvailableAccounts(It.IsAny<int>()))
            .Returns(AsyncEnumerable.Empty<AvailableAccount>());

        _currencyRepository
            .Setup(r => r.GetByCode("USD", It.IsAny<CancellationToken>()))
            .ReturnsAsync(_usd);

        _stockDetailsRepository
            .Setup(r => r.Get(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((StockDetails?)null);

        _stockPriceRepository
            .Setup(r => r.Add(It.IsAny<string>(), It.IsAny<decimal>(), It.IsAny<Currency>(), It.IsAny<DateTime>()))
            .ReturnsAsync((string isin, decimal price, Currency currency, DateTime date) =>
            {
                _seededPrices[date.Date] = price;
                return new StockPrice { Isin = isin, PricePerUnit = price, Currency = currency, Date = date };
            });

        _accountRepository
            .Setup(r => r.AddAccount(It.IsAny<StockAccount>()))
            .Callback<StockAccount>(a => _savedAccounts.Add(a))
            .ReturnsAsync(0);

        _seeder = new StockAccountSeeder(
            _accountRepository.Object,
            _stockAccountRepository.Object,
            _stockPriceRepository.Object,
            _stockDetailsRepository.Object,
            _currencyRepository.Object,
            NullLogger<StockAccountSeeder>.Instance);
    }

    [Fact]
    public async Task Seed_BuysOnlyOnMonthlyInvestmentDays()
    {
        var start = new DateTime(2026, 1, 15);
        var end = new DateTime(2026, 7, 20);

        await _seeder.Seed(userId: 1, start, end, TestContext.Current.CancellationToken);

        var entries = GetStockEntries();

        // Feb–Jul: one buy on day 3 of each month whose investment day falls after the opening day.
        Assert.Equal(6, entries.Count);
        Assert.All(entries, e => Assert.Equal(_investmentDay, e.PostingDate.Day));
        var months = entries.Select(e => new DateTime(e.PostingDate.Year, e.PostingDate.Month, 1)).ToList();
        Assert.Equal(months.Count, months.Distinct().Count());
    }

    [Fact]
    public async Task Seed_BuysOnlyNeverSells()
    {
        var start = new DateTime(2026, 1, 1);
        var end = new DateTime(2026, 6, 30);

        await _seeder.Seed(userId: 1, start, end, TestContext.Current.CancellationToken);

        Assert.All(GetStockEntries(), e => Assert.True(e.ValueChange > 0));
    }

    [Fact]
    public async Task Seed_BuysMatchTheCashInvestmentValueAtThatDaysPrice()
    {
        var start = new DateTime(2026, 1, 1);
        var end = new DateTime(2026, 6, 30);

        await _seeder.Seed(userId: 1, start, end, TestContext.Current.CancellationToken);

        var entries = GetStockEntries();
        Assert.NotEmpty(entries);
        Assert.All(entries, e =>
        {
            var priceThatDay = _seededPrices[e.PostingDate.Date];
            var spent = e.ValueChange * priceThatDay;
            // Units are rounded to 4 dp, so the realised cash spend lands within a cent or two of the target.
            Assert.True(Math.Abs(spent - _monthlyInvestment) < 0.05m,
                $"Buy on {e.PostingDate:yyyy-MM-dd} spent {spent} at {priceThatDay}, expected ~{_monthlyInvestment}.");
        });
    }

    [Fact]
    public async Task Seed_AccumulatesUnitsAcrossPurchases()
    {
        var start = new DateTime(2026, 1, 1);
        var end = new DateTime(2026, 6, 30);

        await _seeder.Seed(userId: 1, start, end, TestContext.Current.CancellationToken);

        var entries = GetStockEntries().OrderBy(e => e.PostingDate).ToList();
        Assert.True(entries.Count > 1);

        // Running Value is cumulative units held, so each later buy leaves more on the books than the prior one.
        decimal expected = 0;
        foreach (var entry in entries)
        {
            expected += entry.ValueChange;
            Assert.Equal(expected, entry.Value);
        }
    }

    [Fact]
    public async Task Seed_SkipsWhenStockAccountAlreadyExists()
    {
        _stockAccountRepository
            .Setup(r => r.GetAvailableAccounts(It.IsAny<int>()))
            .Returns(new[] { new AvailableAccount(1, "Existing") }.ToAsyncEnumerable());

        await _seeder.Seed(userId: 1, new DateTime(2026, 1, 1), new DateTime(2026, 3, 1), TestContext.Current.CancellationToken);

        Assert.Empty(_savedAccounts);
    }

    private List<StockAccountEntry> GetStockEntries() =>
        Assert.Single(_savedAccounts).Entries.ToList();
}