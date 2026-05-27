using FinanceManager.Application.Services.Seeders;
using FinanceManager.Domain.Entities.FinancialAccounts.Currencies;
using FinanceManager.Domain.Entities.Shared.Accounts;
using FinanceManager.Domain.Enums;
using FinanceManager.Domain.Repositories;
using FinanceManager.Domain.Repositories.Account;
using FinanceManager.Domain.ValueObjects;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace FinanceManager.UnitTests.Application.Services.Seeders;

[Collection("Application")]
[Trait("Category", "Unit")]
public class CurrencyAccountSeederTests
{
    private const decimal MonthlySalary = 5_000m;
    private const decimal MonthlyInvestment = 500m;
    private const decimal MonthlyRent = 1_000m;
    private const decimal MonthlyUtilities = 100m;

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
    ];

    private readonly Mock<IFinancialAccountRepository> _accountRepository = new();
    private readonly Mock<ICurrencyAccountRepository<CurrencyAccount>> _currencyAccountRepository = new();
    private readonly Mock<IFinancialLabelsRepository> _labelsRepository = new();
    private readonly List<CurrencyAccount> _savedAccounts = [];
    private readonly CurrencyAccountSeeder _seeder;

    public CurrencyAccountSeederTests()
    {
        _currencyAccountRepository
            .Setup(r => r.GetAvailableAccounts(It.IsAny<int>()))
            .Returns(AsyncEnumerable.Empty<AvailableAccount>());

        _labelsRepository
            .Setup(r => r.GetLabels(It.IsAny<CancellationToken>()))
            .Returns(_seededLabels.ToAsyncEnumerable());

        _accountRepository
            .Setup(r => r.AddAccount(It.IsAny<CurrencyAccount>()))
            .Callback<CurrencyAccount>(a => _savedAccounts.Add(a))
            .ReturnsAsync(0);

        _seeder = new CurrencyAccountSeeder(
            _accountRepository.Object,
            _currencyAccountRepository.Object,
            _labelsRepository.Object,
            NullLogger<CurrencyAccountSeeder>.Instance);
    }

    [Fact]
    public async Task Seed_PaysSalaryOnFirstOfEachMonth()
    {
        var start = new DateTime(2026, 1, 15);
        var end = new DateTime(2026, 4, 20);

        await _seeder.Seed(userId: 1, start, end, TestContext.Current.CancellationToken);

        var cashEntries = GetCashEntries();
        var salaryEntries = cashEntries
            .Where(e => e.PostingDate.Day == 1 && e.ValueChange > 0)
            .ToList();

        Assert.Equal(3, salaryEntries.Count); // Feb 1, Mar 1, Apr 1.
        Assert.All(salaryEntries, e =>
        {
            Assert.Equal(MonthlySalary, e.ValueChange);
            Assert.Contains(e.Labels, l => l.Name == "Salary");
        });
    }

    [Fact]
    public async Task Seed_HasNoIncomeOtherThanSalaryAndOpeningBalance()
    {
        var start = new DateTime(2026, 1, 15);
        var end = new DateTime(2026, 5, 1);

        await _seeder.Seed(userId: 1, start, end, TestContext.Current.CancellationToken);

        var cashEntries = GetCashEntries();
        var positives = cashEntries.Where(e => e.ValueChange > 0).ToList();

        // Allowed positives: one opening balance plus one salary per 1st-of-month within the window.
        Assert.Contains(positives, e => e.PostingDate == start && e.Description == "Opening balance");
        Assert.All(positives.Where(e => e.PostingDate != start),
            e => Assert.Equal(1, e.PostingDate.Day));
    }

    [Fact]
    public async Task Seed_RecordsFixedExpensesOnDay3AndDay4()
    {
        var start = new DateTime(2026, 1, 31);
        var end = new DateTime(2026, 3, 10);

        await _seeder.Seed(userId: 1, start, end, TestContext.Current.CancellationToken);

        var cashEntries = GetCashEntries();

        foreach (var month in new[] { new DateTime(2026, 2, 1), new DateTime(2026, 3, 1) })
        {
            var day3 = cashEntries.SingleOrDefault(e => e.PostingDate == month.AddDays(2));
            Assert.NotNull(day3);
            Assert.Equal(-MonthlyInvestment, day3!.ValueChange);
            Assert.Contains(day3.Labels, l => l.Name == "Investment");

            var day4Entries = cashEntries.Where(e => e.PostingDate == month.AddDays(3)).ToList();
            Assert.Equal(2, day4Entries.Count);
            Assert.Single(day4Entries, e => e.ValueChange == -MonthlyRent && e.Labels.Any(l => l.Name == "Rent"));
            Assert.Single(day4Entries, e => e.ValueChange == -MonthlyUtilities && e.Labels.Any(l => l.Name == "Utilities"));
        }
    }

    [Fact]
    public async Task Seed_MonthlyNegativesNeverReachSalary()
    {
        var start = new DateTime(2026, 1, 15);
        var end = new DateTime(2026, 6, 30);

        await _seeder.Seed(userId: 1, start, end, TestContext.Current.CancellationToken);

        var cashEntries = GetCashEntries();
        var monthlyNegatives = cashEntries
            .Where(e => e.ValueChange < 0)
            .GroupBy(e => new DateTime(e.PostingDate.Year, e.PostingDate.Month, 1))
            .Select(g => new { Month = g.Key, Total = -g.Sum(e => e.ValueChange) });

        Assert.All(monthlyNegatives, m => Assert.True(m.Total < MonthlySalary,
            $"Month {m.Month:yyyy-MM} negatives {m.Total} must be strictly below salary {MonthlySalary}."));
    }

    [Fact]
    public async Task Seed_RandomNegativesUseMerchantDescriptions()
    {
        var start = new DateTime(2026, 1, 15);
        var end = new DateTime(2026, 4, 30);

        await _seeder.Seed(userId: 1, start, end, TestContext.Current.CancellationToken);

        var cashEntries = GetCashEntries();
        var randomNegatives = cashEntries
            .Where(e => e.ValueChange < 0
                && e.PostingDate.Day != 3
                && e.PostingDate.Day != 4)
            .ToList();

        if (randomNegatives.Count == 0) return; // 0-10 per day means a (very unlikely) all-zero run is possible.
        Assert.All(randomNegatives, e => Assert.False(string.IsNullOrWhiteSpace(e.Description)));
        Assert.All(randomNegatives, e => Assert.NotEqual("Rent payment", e.Description));
        Assert.All(randomNegatives, e => Assert.NotEqual("Utilities bill", e.Description));
        Assert.All(randomNegatives, e => Assert.NotEqual("Monthly investment", e.Description));
    }

    [Fact]
    public async Task Seed_SeedsCashAndLoanAccounts()
    {
        var start = new DateTime(2026, 1, 15);
        var end = new DateTime(2026, 3, 15);

        await _seeder.Seed(userId: 1, start, end, TestContext.Current.CancellationToken);

        Assert.Equal(2, _savedAccounts.Count);
        Assert.Contains(_savedAccounts, a => a.AccountType == AccountLabel.Cash);
        Assert.Contains(_savedAccounts, a => a.AccountType == AccountLabel.Loan);
    }

    [Fact]
    public async Task Seed_SkipsWhenAccountsAlreadyExist()
    {
        _currencyAccountRepository
            .Setup(r => r.GetAvailableAccounts(It.IsAny<int>()))
            .Returns(new[] { new AvailableAccount(1, "Existing") }.ToAsyncEnumerable());

        await _seeder.Seed(userId: 1, new DateTime(2026, 1, 15), new DateTime(2026, 2, 15), TestContext.Current.CancellationToken);

        Assert.Empty(_savedAccounts);
    }

    private List<CurrencyAccountEntry> GetCashEntries() =>
        _savedAccounts.Single(a => a.AccountType == AccountLabel.Cash).Entries.ToList();
}
