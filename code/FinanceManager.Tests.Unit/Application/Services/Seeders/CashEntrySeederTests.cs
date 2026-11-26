using FinanceManager.Application.Identity.Seeders;
using FinanceManager.Application.Transactions.Cash.Seeders;
using FinanceManager.Domain.Currencies.Entities;
using FinanceManager.Domain.Identity.Entities;
using FinanceManager.Domain.Identity.Repositories;
using FinanceManager.Domain.Labels.Entities;
using FinanceManager.Domain.Labels.Repositories;
using FinanceManager.Domain.Transactions.Cash.Entities;
using FinanceManager.Domain.Transactions.Cash.Repositories;
using FinanceManager.Domain.Transactions.Shared.Repositories;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace FinanceManager.Tests.Unit.Application.Services.Seeders;

[Collection("Application")]
[Trait("Category", "Unit")]
public class CashEntrySeederTests
{
    private const decimal _monthlySalary = 5_000m;
    private const decimal _monthlyInvestment = 500m;
    private const decimal _monthlyRent = 1_000m;
    private const decimal _monthlyUtilities = 100m;

    private static readonly FinancialLabel[] _seededLabels =
    [
        new() { Id = 1, Name = "Salary" },
        new() { Id = 2, Name = "Investment" },
        new() { Id = 3, Name = "Rent" },
        new() { Id = 4, Name = "Utilities" },
        new() { Id = 5, Name = "Groceries" },
        new() { Id = 6, Name = "Dining Out" },
        new() { Id = 7, Name = "Transportation" },
        new() { Id = 8, Name = "Subscription" },
        new() { Id = 9, Name = "Entertainment" },
        new() { Id = 10, Name = "Healthcare" },
        new() { Id = 11, Name = "Education" },
        new() { Id = 12, Name = "Travel" },
        new() { Id = 13, Name = "Undisclosed Expense" },
        new() { Id = 14, Name = "Undisclosed Income" },
        new() { Id = 15, Name = "Loan" },
    ];

    private readonly Mock<ICashTransactionReader> _cashReader = new();
    private readonly Mock<ITransactionEntryRepository<CashEntry>> _entryRepository = new();
    private readonly Mock<IFinancialLabelsRepository> _labelsRepository = new();
    private readonly List<CashEntry> _savedEntries = [];
    private readonly CashEntrySeeder _seeder;

    public CashEntrySeederTests()
    {
        _cashReader
            .Setup(r => r.HasAnyEntries(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _labelsRepository
            .Setup(r => r.GetLabels(It.IsAny<CancellationToken>()))
            .Returns(_seededLabels.ToAsyncEnumerable());

        _entryRepository
            .Setup(r => r.Add(It.IsAny<IEnumerable<CashEntry>>(), It.IsAny<CancellationToken>()))
            .Callback<IEnumerable<CashEntry>, CancellationToken>((entries, _) => _savedEntries.AddRange(entries))
            .ReturnsAsync(true);

        _seeder = new CashEntrySeeder(
            _cashReader.Object,
            _entryRepository.Object,
            _labelsRepository.Object,
            NullLogger<CashEntrySeeder>.Instance);
    }

    [Fact]
    public async Task Seed_PaysSalaryExactlyOncePerMonth_At5000_WithSalaryLabel()
    {
        var start = new DateTime(2026, 1, 15);
        var end = new DateTime(2026, 7, 20);

        await _seeder.Seed(userId: 1, start, end, new Random(1), TestContext.Current.CancellationToken);

        var cashEntries = GetCashEntries();
        var salaryEntries = cashEntries
            .Where(e => e.Description == "Monthly salary")
            .ToList();

        // Feb–Jul: one salary per complete month in the window.
        Assert.Equal(6, salaryEntries.Count);
        Assert.All(salaryEntries, e =>
        {
            Assert.Equal(1, e.PostingDate.Day);
            Assert.Equal(_monthlySalary, e.ValueChange);
            Assert.Equal("Salary", Assert.Single(e.Labels).Name);
        });

        // Each month appears exactly once — no month carries a second salary.
        var months = salaryEntries
            .Select(e => new DateTime(e.PostingDate.Year, e.PostingDate.Month, 1))
            .ToList();
        Assert.Equal(months.Count, months.Distinct().Count());
    }

    [Fact]
    public async Task Seed_IncomeIsOnlyOpeningBalanceAndSalary()
    {
        var start = new DateTime(2026, 1, 15);
        var end = new DateTime(2026, 5, 1);

        await _seeder.Seed(userId: 1, start, end, new Random(1), TestContext.Current.CancellationToken);

        var cashEntries = GetCashEntries();
        var positives = cashEntries.Where(e => e.ValueChange > 0).ToList();

        // Allowed positives: the opening balance on the first day, plus one salary per 1st-of-month. No loan disbursement.
        Assert.Contains(positives, e => e.PostingDate == start && e.Description == "Opening balance");
        Assert.DoesNotContain(cashEntries, e => e.Description == "Loan disbursement");
        Assert.All(positives.Where(e => e.PostingDate != start),
            e => Assert.Equal(1, e.PostingDate.Day));
    }

    [Fact]
    public async Task Seed_RecordsFixedExpensesOnDay3AndDay4()
    {
        var start = new DateTime(2026, 1, 31);
        var end = new DateTime(2026, 3, 10);

        await _seeder.Seed(userId: 1, start, end, new Random(1), TestContext.Current.CancellationToken);

        var cashEntries = GetCashEntries();

        foreach (var month in new[] { new DateTime(2026, 2, 1), new DateTime(2026, 3, 1) })
        {
            var day3 = cashEntries.SingleOrDefault(e => e.PostingDate == month.AddDays(2));
            Assert.NotNull(day3);
            Assert.Equal(-_monthlyInvestment, day3!.ValueChange);
            Assert.Contains(day3.Labels, l => l.Name == "Investment");

            var day4Entries = cashEntries.Where(e => e.PostingDate == month.AddDays(3)).ToList();
            Assert.Equal(2, day4Entries.Count);
            Assert.Single(day4Entries, e => e.ValueChange == -_monthlyRent && e.Labels.Any(l => l.Name == "Rent"));
            Assert.Single(day4Entries, e => e.ValueChange == -_monthlyUtilities && e.Labels.Any(l => l.Name == "Utilities"));
        }
    }

    [Fact]
    public async Task Seed_MonthlyNegativesNeverReachSalary()
    {
        var start = new DateTime(2026, 1, 15);
        var end = new DateTime(2026, 6, 30);

        await _seeder.Seed(userId: 1, start, end, new Random(1), TestContext.Current.CancellationToken);

        var cashEntries = GetCashEntries();
        var monthlyNegatives = cashEntries
            .Where(e => e.ValueChange < 0 && e.CurrencyId == DefaultCurrency.PLN.Id)
            .GroupBy(e => new DateTime(e.PostingDate.Year, e.PostingDate.Month, 1))
            .Select(g => new { Month = g.Key, Total = -g.Sum(e => e.ValueChange) });

        Assert.All(monthlyNegatives, m => Assert.True(m.Total < _monthlySalary,
            $"Month {m.Month:yyyy-MM} negatives {m.Total} must be strictly below salary {_monthlySalary}."));
    }

    [Fact]
    public async Task Seed_RandomNegativesUseMerchantDescriptions()
    {
        var start = new DateTime(2026, 1, 15);
        var end = new DateTime(2026, 4, 30);

        await _seeder.Seed(userId: 1, start, end, new Random(1), TestContext.Current.CancellationToken);

        var cashEntries = GetCashEntries();
        // Exclude the fixed transaction days (stock day 3, rent/utilities day 4, bond day 5, loan repayment day 6).
        var randomNegatives = cashEntries
            .Where(e => e.ValueChange < 0
                && e.PostingDate.Day is not (3 or 4 or 5 or 6))
            .ToList();

        if (randomNegatives.Count == 0) return; // 0-10 per day means a (very unlikely) all-zero run is possible.
        Assert.All(randomNegatives, e => Assert.False(string.IsNullOrWhiteSpace(e.Description)));
        Assert.All(randomNegatives, e => Assert.NotEqual("Rent payment", e.Description));
        Assert.All(randomNegatives, e => Assert.NotEqual("Utilities bill", e.Description));
        Assert.All(randomNegatives, e => Assert.NotEqual("Loan repayment", e.Description));
    }

    [Fact]
    public async Task Seed_RandomNegativesAtSameMerchantNeverLookRecurring()
    {
        // Regression for #904: near-equal amounts at one merchant coincidentally formed quarterly
        // patterns, so the demo Subscriptions page listed "Tram pass" (and others) twice.
        var start = new DateTime(2024, 9, 1);
        var end = new DateTime(2026, 10, 1);

        await _seeder.Seed(userId: 1, start, end, new Random(1), TestContext.Current.CancellationToken);

        var fixedDescriptions = new[] { "Opening balance", "Monthly salary", "Rent payment", "Utilities bill", "Loan repayment" };
        var randomNegatives = GetCashEntries()
            .Where(e => e.ValueChange < 0
                && !fixedDescriptions.Contains(e.Description)
                && !e.Description.StartsWith("Investment", StringComparison.Ordinal))
            .GroupBy(e => e.Description)
            .ToList();

        Assert.NotEmpty(randomNegatives);
        foreach (var merchant in randomNegatives)
        {
            var amounts = merchant.Select(e => -e.ValueChange).ToList();
            for (var i = 0; i < amounts.Count; i++)
                for (var j = i + 1; j < amounts.Count; j++)
                    Assert.True(
                        Math.Abs(amounts[i] - amounts[j]) > Math.Max(amounts[i], amounts[j]) * 0.12m,
                        $"{merchant.Key}: amounts {amounts[i]} and {amounts[j]} do not exceed the seeder's 12% amount gap.");
        }
    }

    [Fact]
    public async Task Seed_WritesPooledEntriesOwnedByTheUser()
    {
        await _seeder.Seed(userId: -12, new DateTime(2026, 1, 15), new DateTime(2026, 3, 15), new Random(1), TestContext.Current.CancellationToken);

        Assert.NotEmpty(_savedEntries);
        Assert.All(_savedEntries, e =>
        {
            Assert.Equal(-12, e.UserId);
            Assert.True(e.CurrencyId == DefaultCurrency.PLN.Id || e.CurrencyId == DefaultCurrency.USD.Id);
        });
        Assert.Contains(_savedEntries, e => e.CurrencyId == DefaultCurrency.PLN.Id);
        _entryRepository.Verify(r => r.Add(It.IsAny<IEnumerable<CashEntry>>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData(true, 1)]
    [InlineData(false, 0)]
    public async Task Seed_SkipsWhenTheUserAlreadyHasEntries_OrIsNotAUser(bool hasEntries, int userId)
    {
        _cashReader.Setup(r => r.HasAnyEntries(It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync(hasEntries);

        await _seeder.Seed(userId, new DateTime(2026, 1, 15), new DateTime(2026, 2, 15), new Random(1), TestContext.Current.CancellationToken);

        Assert.Empty(_savedEntries);
    }

    [Fact]
    public async Task Seed_DoesNotBorrowALoan_AndRepaysItMonthlyAsAPlainOutflow()
    {
        var start = new DateTime(2026, 1, 31);
        var end = new DateTime(2026, 4, 10);

        await _seeder.Seed(userId: 1, start, end, new Random(1), TestContext.Current.CancellationToken);

        Assert.DoesNotContain(GetCashEntries(), e => e.Description is "Loan disbursement" or "Loan principal");
        foreach (var month in new[] { new DateTime(2026, 2, 1), new DateTime(2026, 3, 1), new DateTime(2026, 4, 1) })
        {
            var repaymentDay = month.AddDays(GuestInvestmentPlan.LoanRepaymentDayOfMonth - 1);

            var repayment = GetCashEntries().Single(e => e.PostingDate == repaymentDay && e.Description == "Loan repayment");
            Assert.Equal(-GuestInvestmentPlan.LoanMonthlyRepayment, repayment.ValueChange);
            Assert.Contains(repayment.Labels, l => l.Name == "Loan");
        }
    }

    [Fact]
    public async Task Seed_PooledBalanceNeverGoesNegative()
    {
        // The opening balance equals one salary and each month's outflows stay below the salary, so with no disbursement
        // the PLN balance never goes negative; the small USD card debt is the demo's only liability.
        var start = new DateTime(2025, 12, 15);
        var end = new DateTime(2026, 6, 15);

        await _seeder.Seed(userId: 1, start, end, new Random(1), TestContext.Current.CancellationToken);

        var balance = 0m;
        foreach (var entry in GetCashEntries().Where(e => e.CurrencyId == DefaultCurrency.PLN.Id).OrderBy(e => e.PostingDate).ThenByDescending(e => e.ValueChange))
        {
            balance += entry.ValueChange;
            Assert.True(balance >= 0m, $"Pooled balance {balance} went negative on {entry.PostingDate:d} ({entry.Description}).");
        }
    }

    [Fact]
    public async Task Seed_BuysBondFromCashEachMonthWithInvestmentLabel()
    {
        var start = new DateTime(2026, 1, 31);
        var end = new DateTime(2026, 4, 10);

        await _seeder.Seed(userId: 1, start, end, new Random(1), TestContext.Current.CancellationToken);

        foreach (var month in new[] { new DateTime(2026, 2, 1), new DateTime(2026, 3, 1), new DateTime(2026, 4, 1) })
        {
            var bondDay = month.AddDays(GuestInvestmentPlan.BondDayOfMonth - 1);
            var bondPurchase = GetCashEntries().Single(e => e.PostingDate == bondDay && e.ValueChange == -GuestInvestmentPlan.BondMonthlyAmount);
            Assert.Contains(bondPurchase.Labels, l => l.Name == "Investment");
            Assert.False(string.IsNullOrWhiteSpace(bondPurchase.Description));
        }
    }

    [Fact]
    public async Task Seed_WithRandomsFromTheSameSeed_ProducesIdenticalCashEntries()
    {
        var first = await SeedCashEntries(new GuestDemoSeed(new DateOnly(2026, 10, 14)));
        var second = await SeedCashEntries(new GuestDemoSeed(new DateOnly(2026, 10, 14)));

        Assert.Equal(first, second);
    }

    [Fact]
    public async Task Seed_WithRandomsFromDifferentSeeds_ProducesDifferentCashEntries()
    {
        var first = await SeedCashEntries(new GuestDemoSeed(new DateOnly(2026, 10, 14)));
        var second = await SeedCashEntries(new GuestDemoSeed(new DateOnly(2026, 10, 15)));

        Assert.NotEqual(first, second);
    }

    private async Task<List<string>> SeedCashEntries(GuestDemoSeed demoSeed)
    {
        _savedEntries.Clear();
        var end = demoSeed.Date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);

        await _seeder.Seed(userId: 1, end.AddMonths(-6), end, demoSeed.CreateRandom(GuestDemoSeed.CashSalt), TestContext.Current.CancellationToken);

        return GetCashEntries()
            .Select(e => $"{e.PostingDate:O}|{e.ValueChange}|{e.Description}|{string.Join(',', e.Labels.Select(l => l.Name))}")
            .ToList();
    }

    private List<CashEntry> GetCashEntries() => _savedEntries;
}