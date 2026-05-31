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
    public async Task Seed_PaysSalaryExactlyOncePerMonth_At5000_WithSalaryLabel()
    {
        var start = new DateTime(2026, 1, 15);
        var end = new DateTime(2026, 7, 20);

        await _seeder.Seed(userId: 1, start, end, TestContext.Current.CancellationToken);

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
    public async Task Seed_IncomeIsOnlyOpeningBalanceLoanDisbursementAndSalary()
    {
        var start = new DateTime(2026, 1, 15);
        var end = new DateTime(2026, 5, 1);

        await _seeder.Seed(userId: 1, start, end, TestContext.Current.CancellationToken);

        var cashEntries = GetCashEntries();
        var positives = cashEntries.Where(e => e.ValueChange > 0).ToList();

        // Allowed positives: opening balance and the loan disbursement on the first day, plus one salary per 1st-of-month.
        Assert.Contains(positives, e => e.PostingDate == start && e.Description == "Opening balance");
        Assert.Contains(positives, e => e.PostingDate == start && e.Description == "Loan disbursement");
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

        await _seeder.Seed(userId: 1, start, end, TestContext.Current.CancellationToken);

        var cashEntries = GetCashEntries();
        var monthlyNegatives = cashEntries
            .Where(e => e.ValueChange < 0)
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

        await _seeder.Seed(userId: 1, start, end, TestContext.Current.CancellationToken);

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

    [Fact]
    public async Task Seed_BorrowsLoanOnFirstDayAndCreditsCashWithSameAmount()
    {
        var start = new DateTime(2026, 1, 15);
        var end = new DateTime(2026, 3, 15);

        await _seeder.Seed(userId: 1, start, end, TestContext.Current.CancellationToken);

        var cashDisbursement = GetCashEntries().Single(e => e.Description == "Loan disbursement");
        Assert.Equal(start, cashDisbursement.PostingDate);
        Assert.Equal(GuestInvestmentPlan.LoanPrincipal, cashDisbursement.ValueChange);
        Assert.Contains(cashDisbursement.Labels, l => l.Name == "Loan");

        var loanPrincipal = GetLoanEntries().Single(e => e.Description == "Loan principal");
        Assert.Equal(start, loanPrincipal.PostingDate);
        // The loan books the debt as a negative; cash receives the same magnitude as a positive inflow.
        Assert.Equal(-cashDisbursement.ValueChange, loanPrincipal.ValueChange);
        Assert.Contains(loanPrincipal.Labels, l => l.Name == "Loan");
    }

    [Fact]
    public async Task Seed_RepaysLoanMonthlyInBothCashAndLoanAccounts()
    {
        var start = new DateTime(2026, 1, 31);
        var end = new DateTime(2026, 4, 10);

        await _seeder.Seed(userId: 1, start, end, TestContext.Current.CancellationToken);

        foreach (var month in new[] { new DateTime(2026, 2, 1), new DateTime(2026, 3, 1), new DateTime(2026, 4, 1) })
        {
            var repaymentDay = month.AddDays(GuestInvestmentPlan.LoanRepaymentDayOfMonth - 1);

            var cashRepayment = GetCashEntries().Single(e => e.PostingDate == repaymentDay && e.Description == "Loan repayment");
            Assert.Equal(-GuestInvestmentPlan.LoanMonthlyRepayment, cashRepayment.ValueChange);
            Assert.Contains(cashRepayment.Labels, l => l.Name == "Loan");

            var loanRepayment = GetLoanEntries().Single(e => e.PostingDate == repaymentDay && e.Description == "Loan repayment");
            Assert.Equal(GuestInvestmentPlan.LoanMonthlyRepayment, loanRepayment.ValueChange);
        }
    }

    [Fact]
    public async Task Seed_BuysBondFromCashEachMonthWithInvestmentLabel()
    {
        var start = new DateTime(2026, 1, 31);
        var end = new DateTime(2026, 4, 10);

        await _seeder.Seed(userId: 1, start, end, TestContext.Current.CancellationToken);

        foreach (var month in new[] { new DateTime(2026, 2, 1), new DateTime(2026, 3, 1), new DateTime(2026, 4, 1) })
        {
            var bondDay = month.AddDays(GuestInvestmentPlan.BondDayOfMonth - 1);
            var bondPurchase = GetCashEntries().Single(e => e.PostingDate == bondDay && e.ValueChange == -GuestInvestmentPlan.BondMonthlyAmount);
            Assert.Contains(bondPurchase.Labels, l => l.Name == "Investment");
            Assert.False(string.IsNullOrWhiteSpace(bondPurchase.Description));
        }
    }

    private List<CurrencyAccountEntry> GetCashEntries() =>
        _savedAccounts.Single(a => a.AccountType == AccountLabel.Cash).Entries.ToList();

    private List<CurrencyAccountEntry> GetLoanEntries() =>
        _savedAccounts.Single(a => a.AccountType == AccountLabel.Loan).Entries.ToList();
}
