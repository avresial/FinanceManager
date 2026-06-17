using FinanceManager.Application.FinancialAccounts.Bond.Seeders;
using FinanceManager.Application.FinancialAccounts.Stock.Seeders;
using FinanceManager.Application.Identity.Seeders;
using FinanceManager.Domain.Enums;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.FinancialAccounts.Currencies.Repositories;
using FinanceManager.Domain.FinancialAccounts.Shared.Entities;
using FinanceManager.Domain.FinancialAccounts.Shared.Repositories;
using FinanceManager.Domain.Identity.Repositories;
using FinanceManager.Domain.Labels.Repositories;
using FinanceManager.Domain.Repositories;
using Microsoft.Extensions.Logging;

namespace FinanceManager.Application.FinancialAccounts.Currencies.Seeders;

public class CurrencyAccountSeeder(
    IFinancialAccountRepository accountRepository,
    ICurrencyAccountRepository<CurrencyAccount> currencyAccountRepository,
    IFinancialLabelsRepository financialLabelsRepository,
    ILogger<CurrencyAccountSeeder> logger)
{
    private const decimal _monthlySalary = 5_000m;
    private const decimal _monthlyRent = 1_000m;
    private const decimal _monthlyUtilities = 100m;
    private const int _maxRandomTransactionsPerDay = 10;
    private const int _maxRandomTransactionAmount = 150;

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
        await SeedLoanAccount(userId, labelByName, start, end, cancellationToken);
    }

    private async Task SeedCashAccount(int userId, Dictionary<string, FinancialLabel> labelByName, DateTime start, DateTime end, CancellationToken cancellationToken)
    {
        var account = new CurrencyAccount(userId, 0, $"{AccountLabel.Cash} 1", AccountLabel.Cash);

        var salaryLabels = LabelsFor(labelByName, "Salary");
        var investmentLabels = LabelsFor(labelByName, "Investment");
        var rentLabels = LabelsFor(labelByName, "Rent");
        var utilitiesLabels = LabelsFor(labelByName, "Utilities");
        var loanLabels = LabelsFor(labelByName, "Loan");

        // Opening balance equals one month's salary so the partial first month can absorb random expenses
        // before the first day-1 paycheck lands.
        account.AddEntry(new AddCurrencyEntryDto(start, _monthlySalary, "Opening balance", null, []), false);

        // The loan is taken out on the guest's very first day: its proceeds land in cash here while the loan
        // account books the matching debt (see SeedLoanAccount).
        account.AddEntry(new AddCurrencyEntryDto(start, GuestInvestmentPlan.LoanPrincipal, "Loan disbursement", null, loanLabels), false);

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
                    account.AddEntry(new AddCurrencyEntryDto(date, _monthlySalary, "Monthly salary", null, salaryLabels), false);
                    break;
                case GuestInvestmentPlan.StockDayOfMonth:
                    // Pays for the matching stock purchase StockAccountSeeder books on the same day.
                    account.AddEntry(new AddCurrencyEntryDto(date, -GuestInvestmentPlan.StockMonthlyAmount, "Investment — iShares Core S&P 500 ETF (CSPX.LON)", null, investmentLabels), false);
                    negativesThisMonth += GuestInvestmentPlan.StockMonthlyAmount;
                    break;
                case 4:
                    account.AddEntry(new AddCurrencyEntryDto(date, -_monthlyRent, "Rent payment", null, rentLabels), false);
                    account.AddEntry(new AddCurrencyEntryDto(date, -_monthlyUtilities, "Utilities bill", null, utilitiesLabels), false);
                    negativesThisMonth += _monthlyRent + _monthlyUtilities;
                    break;
                case GuestInvestmentPlan.BondDayOfMonth:
                    // Pays for the matching bond purchase BondAccountSeeder books on the same day.
                    account.AddEntry(new AddCurrencyEntryDto(date, -GuestInvestmentPlan.BondMonthlyAmount, "Investment — Polish Treasury inflation bond", null, investmentLabels), false);
                    negativesThisMonth += GuestInvestmentPlan.BondMonthlyAmount;
                    break;
                case GuestInvestmentPlan.LoanRepaymentDayOfMonth:
                    // Mirrors the loan repayment SeedLoanAccount books on the same day.
                    account.AddEntry(new AddCurrencyEntryDto(date, -GuestInvestmentPlan.LoanMonthlyRepayment, "Loan repayment", null, loanLabels), false);
                    negativesThisMonth += GuestInvestmentPlan.LoanMonthlyRepayment;
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
        var transactions = Random.Shared.Next(0, _maxRandomTransactionsPerDay + 1);
        for (var i = 0; i < transactions; i++)
        {
            // Rule from #240: total negatives within a month must stay strictly below the salary.
            var remaining = _monthlySalary - 1m - negativesThisMonth;
            if (remaining < 1m) break;

            var max = (int)Math.Floor(Math.Min(_maxRandomTransactionAmount, remaining));
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

    private async Task SeedLoanAccount(int userId, Dictionary<string, FinancialLabel> labelByName, DateTime start, DateTime end, CancellationToken cancellationToken)
    {
        var account = new CurrencyAccount(userId, 0, $"{AccountLabel.Loan} 1", AccountLabel.Loan);
        var loanLabels = LabelsFor(labelByName, "Loan");

        // Borrowed in full on the guest's first day; the matching cash inflow is booked by SeedCashAccount.
        account.AddEntry(new AddCurrencyEntryDto(start, -GuestInvestmentPlan.LoanPrincipal, "Loan principal", null, loanLabels), false);

        // Equal monthly repayments shrink the debt toward zero, each mirrored by a cash outflow on the same day.
        for (var date = start.AddDays(1); date <= end; date = date.AddDays(1))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!GuestInvestmentPlan.IsLoanRepaymentDay(date)) continue;

            account.AddEntry(new AddCurrencyEntryDto(date, GuestInvestmentPlan.LoanMonthlyRepayment, "Loan repayment", null, loanLabels), false);
        }

        account.RecalculateEntryValues(account.Entries.Count - 1);
        await accountRepository.AddAccount(account);
    }

    private async Task<List<FinancialLabel>> GetSeedableLabels(CancellationToken cancellationToken)
    {
        var all = await financialLabelsRepository.GetLabels(cancellationToken).ToListAsync(cancellationToken);
        // The NoMatch sentinel is reserved for the AI label setter to mark "no real category fit" — it must never be seeded onto demo data.
        return all.Where(l => !string.Equals(l.Name, WellKnownFinancialLabels.NoMatch, StringComparison.Ordinal)).ToList();
    }
}