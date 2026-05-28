using FinanceManager.Domain.Entities.FinancialAccounts.Currencies;
using FinanceManager.Domain.Entities.Shared.Accounts;
using FinanceManager.Domain.Enums;
using FinanceManager.Domain.Repositories;
using FinanceManager.Domain.Repositories.Account;
using Microsoft.Extensions.Logging;

namespace FinanceManager.Application.Services.Seeders;

public class CurrencyAccountSeeder(
    IFinancialAccountRepository accountRepository,
    ICurrencyAccountRepository<CurrencyAccount> currencyAccountRepository,
    IFinancialLabelsRepository financialLabelsRepository,
    ILogger<CurrencyAccountSeeder> logger)
{
    private const decimal MonthlySalary = 5_000m;
    private const decimal MonthlyRent = 1_000m;
    private const decimal MonthlyUtilities = 100m;
    private const int MaxRandomTransactionsPerDay = 10;
    private const int MaxRandomTransactionAmount = 150;

    // Income labels can only appear on entries whose value change is positive (money coming in).
    private static readonly HashSet<string> _incomeLabels = new(StringComparer.OrdinalIgnoreCase)
    {
        "Salary",
        "Undisclosed Income",
    };

    // Expense labels can only appear on entries whose value change is negative (money going out).
    private static readonly HashSet<string> _expenseLabels = new(StringComparer.OrdinalIgnoreCase)
    {
        "Groceries",
        "Rent",
        "Utilities",
        "Entertainment",
        "Subscription",
        "Transportation",
        "Healthcare",
        "Education",
        "Dining Out",
        "Travel",
        "Investment",
        "Undisclosed Expense",
    };

    private record FakeMerchant(string Description, string LabelName);

    private static readonly FakeMerchant[] _fakeMerchants =
    [
        new("Biedronka", "Groceries"),
        new("Lidl", "Groceries"),
        new("Carrefour", "Groceries"),
        new("Auchan", "Groceries"),
        new("Żabka", "Groceries"),
        new("Kaufland", "Groceries"),
        new("Pizza Hut", "Dining Out"),
        new("McDonald's", "Dining Out"),
        new("KFC", "Dining Out"),
        new("Sphinx", "Dining Out"),
        new("Starbucks", "Dining Out"),
        new("Uber", "Transportation"),
        new("Bolt", "Transportation"),
        new("PKP train ticket", "Transportation"),
        new("Tram pass", "Transportation"),
        new("Orlen fuel", "Transportation"),
        new("Netflix", "Subscription"),
        new("Spotify", "Subscription"),
        new("YouTube Premium", "Subscription"),
        new("Disney+", "Subscription"),
        new("Cinema City", "Entertainment"),
        new("Bowling night", "Entertainment"),
        new("Concert ticket", "Entertainment"),
        new("Steam game", "Entertainment"),
        new("Apteka pharmacy", "Healthcare"),
        new("Doctor visit", "Healthcare"),
        new("Dentist", "Healthcare"),
        new("Empik bookstore", "Education"),
        new("Online course", "Education"),
        new("Hotel booking", "Travel"),
        new("Flight ticket", "Travel"),
        new("Airbnb stay", "Travel"),
    ];

    public async Task Seed(int userId, DateTime start, DateTime end, CancellationToken cancellationToken = default)
    {
        if (await currencyAccountRepository.GetAvailableAccounts(userId).AnyAsync(cancellationToken)) return;

        var labels = await GetSeedableLabels(cancellationToken);
        var labelByName = labels.ToDictionary(l => l.Name, StringComparer.OrdinalIgnoreCase);

        logger.LogTrace("Seeding cash currency account.");
        await SeedCashAccount(userId, labelByName, start, end, cancellationToken);

        logger.LogTrace("Seeding loan currency account.");
        await SeedLoanAccount(userId, labels, start, end, cancellationToken);
    }

    private async Task SeedCashAccount(int userId, Dictionary<string, FinancialLabel> labelByName, DateTime start, DateTime end, CancellationToken cancellationToken)
    {
        var account = new CurrencyAccount(userId, 0, $"{AccountLabel.Cash} 1", AccountLabel.Cash);

        // Opening balance equals one month's salary so the partial first month can absorb random expenses
        // before the first day-1 paycheck lands.
        account.AddEntry(new AddCurrencyEntryDto(start, MonthlySalary, "Opening balance", null, []), false);

        var salaryLabels = LabelsFor(labelByName, "Salary");
        var investmentLabels = LabelsFor(labelByName, "Investment");
        var rentLabels = LabelsFor(labelByName, "Rent");
        var utilitiesLabels = LabelsFor(labelByName, "Utilities");

        var currentMonth = new DateTime(start.Year, start.Month, 1);
        decimal negativesThisMonth = 0m;

        for (var date = start.AddDays(1); date <= end; date = date.AddDays(1))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var monthStart = new DateTime(date.Year, date.Month, 1);
            if (monthStart != currentMonth)
            {
                currentMonth = monthStart;
                negativesThisMonth = 0m;
            }

            switch (date.Day)
            {
                case 1:
                    account.AddEntry(new AddCurrencyEntryDto(date, MonthlySalary, "Monthly salary", null, salaryLabels), false);
                    break;
                case GuestInvestmentPlan.DayOfMonth:
                    account.AddEntry(new AddCurrencyEntryDto(date, -GuestInvestmentPlan.MonthlyAmount, "Monthly investment", null, investmentLabels), false);
                    negativesThisMonth += GuestInvestmentPlan.MonthlyAmount;
                    break;
                case 4:
                    account.AddEntry(new AddCurrencyEntryDto(date, -MonthlyRent, "Rent payment", null, rentLabels), false);
                    account.AddEntry(new AddCurrencyEntryDto(date, -MonthlyUtilities, "Utilities bill", null, utilitiesLabels), false);
                    negativesThisMonth += MonthlyRent + MonthlyUtilities;
                    break;
                default:
                    negativesThisMonth = AddRandomNegatives(account, date, labelByName, negativesThisMonth);
                    break;
            }
        }

        account.RecalculateEntryValues(account.Entries.Count - 1);
        await accountRepository.AddAccount(account);
    }

    private static decimal AddRandomNegatives(CurrencyAccount account, DateTime date, Dictionary<string, FinancialLabel> labelByName, decimal negativesThisMonth)
    {
        var transactions = Random.Shared.Next(0, MaxRandomTransactionsPerDay + 1);
        for (var i = 0; i < transactions; i++)
        {
            // Rule from #240: total negatives within a month must stay strictly below the salary.
            var remaining = MonthlySalary - 1m - negativesThisMonth;
            if (remaining < 1m) break;

            var max = (int)Math.Floor(Math.Min(MaxRandomTransactionAmount, remaining));
            if (max < 1) break;
            var amount = Random.Shared.Next(1, max + 1);

            var merchant = _fakeMerchants[Random.Shared.Next(_fakeMerchants.Length)];
            var labels = LabelsFor(labelByName, merchant.LabelName);
            account.AddEntry(new AddCurrencyEntryDto(date, -amount, merchant.Description, null, labels), false);
            negativesThisMonth += amount;
        }
        return negativesThisMonth;
    }

    private static List<FinancialLabel> LabelsFor(Dictionary<string, FinancialLabel> labelByName, string name) =>
        labelByName.TryGetValue(name, out var label) ? [label] : [];

    private async Task SeedLoanAccount(int userId, IReadOnlyList<FinancialLabel> labels, DateTime start, DateTime end, CancellationToken cancellationToken)
    {
        var account = new CurrencyAccount(userId, 0, $"{AccountLabel.Loan} 1", AccountLabel.Loan);
        var days = Math.Max(1, (int)(end - start).TotalDays);

        var opening = -Random.Shared.Next(days * 100, days * 200);
        account.AddEntry(new AddCurrencyEntryDto(start, opening, "Loan principal", null, []), false);

        for (var date = start.AddDays(1); date <= end; date = date.AddDays(1))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var repayment = Random.Shared.Next(10, 100);
            account.AddEntry(new AddCurrencyEntryDto(date, repayment, "Repayment", null, PickLabels(labels, repayment)), false);
        }

        account.RecalculateEntryValues(account.Entries.Count - 1);
        await accountRepository.AddAccount(account);
    }

    private static List<FinancialLabel> PickLabels(IReadOnlyList<FinancialLabel> pool, decimal valueChange)
    {
        var candidates = pool
            .Where(l => IsLabelCompatibleWithSign(l.Name, valueChange))
            .ToList();
        if (candidates.Count == 0) return [];

        var count = Math.Min(Random.Shared.Next(0, 100) < 25 ? 2 : 1, candidates.Count);
        var result = new List<FinancialLabel>(count);
        while (result.Count < count)
        {
            var choice = candidates[Random.Shared.Next(candidates.Count)];
            if (!result.Contains(choice)) result.Add(choice);
        }
        return result;
    }

    private static bool IsLabelCompatibleWithSign(string labelName, decimal valueChange)
    {
        if (_incomeLabels.Contains(labelName)) return valueChange > 0;
        if (_expenseLabels.Contains(labelName)) return valueChange < 0;
        // Neutral labels (anything not in either set) are usable for either direction.
        return true;
    }

    private async Task<List<FinancialLabel>> GetSeedableLabels(CancellationToken cancellationToken)
    {
        var all = await financialLabelsRepository.GetLabels(cancellationToken).ToListAsync(cancellationToken);
        // The NoMatch sentinel is reserved for the AI label setter to mark "no real category fit" — it must never be seeded onto demo data.
        return all.Where(l => !string.Equals(l.Name, WellKnownFinancialLabels.NoMatch, StringComparison.Ordinal)).ToList();
    }
}
