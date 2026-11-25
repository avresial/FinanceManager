using FinanceManager.Application.Identity.Seeders;
using FinanceManager.Domain.Currencies.Entities;
using FinanceManager.Domain.Identity.Entities;
using FinanceManager.Domain.Identity.Repositories;
using FinanceManager.Domain.Labels.Entities;
using FinanceManager.Domain.Labels.Repositories;
using FinanceManager.Domain.Shared;
using FinanceManager.Domain.Transactions.Cash.Entities;
using FinanceManager.Domain.Transactions.Cash.Repositories;
using FinanceManager.Domain.Transactions.Shared.Repositories;
using Microsoft.Extensions.Logging;

namespace FinanceManager.Application.Transactions.Cash.Seeders;

public class CashEntrySeeder(
    ICashTransactionReader cashTransactionReader,
    ITransactionEntryRepository<CashEntry> currencyEntryRepository,
    IFinancialLabelsRepository financialLabelsRepository,
    ILogger<CashEntrySeeder> logger)
{
    private const decimal _monthlySalary = 5_000m;
    private const decimal _monthlyRent = 1_000m;
    private const decimal _monthlyUtilities = 100m;
    private const int _maxRandomTransactionsPerDay = 10;
    private const int _maxRandomTransactionAmount = 150;
    private const int _maxAmountPickAttempts = 10;
    // Wider than the detector's 5% amount tolerance (two amounts can sit 10% apart and still cluster).
    private const double _distinctAmountGap = 0.12;

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

    public async Task Seed(int userId, DateTime start, DateTime end, Random random, CancellationToken cancellationToken = default)
    {
        if (userId == 0 || await cashTransactionReader.HasAnyEntries(userId, cancellationToken)) return;

        var labels = await GetSeedableLabels(cancellationToken);
        var labelByName = labels.ToDictionary(l => l.Name, StringComparer.OrdinalIgnoreCase);

        logger.LogTrace("Seeding pooled cash entries.");
        await SeedCash(userId, labelByName, start, end, random, cancellationToken);
    }

    // The demo owns one pooled PLN cash stream (plus a small USD card debt): no account, no loan account. The loan shows up as its monthly repayments,
    // a plain recurring outflow, so there is no disbursement inflow to inflate net worth. Monthly outflows stay below the
    // salary and the opening balance equals one salary, so the pooled balance never goes negative.
    private async Task SeedCash(int userId, Dictionary<string, FinancialLabel> labelByName, DateTime start, DateTime end, Random random, CancellationToken cancellationToken)
    {
        List<CashEntry> entries = [];
        void Add(DateTime date, decimal valueChange, string description, List<FinancialLabel> labels) =>
            entries.Add(new CashEntry(null, 0, date, 0m, valueChange)
            {
                UserId = userId,
                CurrencyId = DefaultCurrency.PLN.Id,
                Description = description,
                Labels = labels
            });

        var salaryLabels = LabelsFor(labelByName, "Salary");
        var investmentLabels = LabelsFor(labelByName, "Investment");
        var rentLabels = LabelsFor(labelByName, "Rent");
        var utilitiesLabels = LabelsFor(labelByName, "Utilities");
        var loanLabels = LabelsFor(labelByName, "Loan");

        // Opening balance equals one month's salary so the partial first month can absorb random expenses
        // before the first day-1 paycheck lands.
        Add(start, _monthlySalary, "Opening balance", []);

        var currentMonth = CalendarMonth.Of(start);
        decimal negativesThisMonth = 0m;
        var randomAmountsByMerchant = new Dictionary<string, List<int>>(StringComparer.Ordinal);

        for (var date = start.AddDays(1); date <= end; date = date.AddDays(1))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var month = CalendarMonth.Of(date);
            if (month != currentMonth)
            {
                currentMonth = month;
                negativesThisMonth = 0m;
            }

            switch (date.Day)
            {
                case 1:
                    Add(date, _monthlySalary, "Monthly salary", salaryLabels);
                    break;
                case GuestInvestmentPlan.StockDayOfMonth:
                    // Pays for the matching stock purchase StockAccountSeeder books on the same day.
                    Add(date, -GuestInvestmentPlan.StockMonthlyAmount, "Investment — iShares Core S&P 500 ETF (CSPX.LON)", investmentLabels);
                    negativesThisMonth += GuestInvestmentPlan.StockMonthlyAmount;
                    break;
                case 4:
                    Add(date, -_monthlyRent, "Rent payment", rentLabels);
                    Add(date, -_monthlyUtilities, "Utilities bill", utilitiesLabels);
                    negativesThisMonth += _monthlyRent + _monthlyUtilities;
                    break;
                case GuestInvestmentPlan.BondDayOfMonth:
                    // Pays for the matching bond purchase BondEntrySeeder books on the same day.
                    Add(date, -GuestInvestmentPlan.BondMonthlyAmount, "Investment — Polish Treasury inflation bond", investmentLabels);
                    negativesThisMonth += GuestInvestmentPlan.BondMonthlyAmount;
                    break;
                case GuestInvestmentPlan.LoanRepaymentDayOfMonth:
                    Add(date, -GuestInvestmentPlan.LoanMonthlyRepayment, "Loan repayment", loanLabels);
                    negativesThisMonth += GuestInvestmentPlan.LoanMonthlyRepayment;
                    break;
                default:
                    negativesThisMonth = AddRandomNegatives(Add, date, labelByName, negativesThisMonth, randomAmountsByMerchant, random);
                    break;
            }
        }

        AddForeignCardDebt(entries, userId, start, end, LabelsFor(labelByName, "Travel"));

        await currencyEntryRepository.Add(entries, cancellationToken);
    }

    // A small card balance in a second currency that is never paid back inside the window, so the demo's pooled USD balance
    // stays negative and the Liabilities page has something to show. PLN stays positive, so net worth is barely affected.
    private static void AddForeignCardDebt(List<CashEntry> entries, int userId, DateTime start, DateTime end, List<FinancialLabel> labels)
    {
        foreach (var (date, amount, description) in new (DateTime, decimal, string)[]
        {
            (start.AddDays(15), -240m, "Conference registration (USD card)"),
            (end.AddDays(-12), -85m, "Online software license (USD card)"),
        })
        {
            entries.Add(new CashEntry(null, 0, date, 0m, amount)
            {
                UserId = userId,
                CurrencyId = DefaultCurrency.USD.Id,
                Description = description,
                Labels = labels
            });
        }
    }

    private static decimal AddRandomNegatives(
        Action<DateTime, decimal, string, List<FinancialLabel>> add,
        DateTime date,
        Dictionary<string, FinancialLabel> labelByName,
        decimal negativesThisMonth,
        Dictionary<string, List<int>> randomAmountsByMerchant,
        Random random)
    {
        var transactions = random.Next(0, _maxRandomTransactionsPerDay + 1);
        for (var i = 0; i < transactions; i++)
        {
            // Rule from #240: total negatives within a month must stay strictly below the salary.
            var remaining = _monthlySalary - 1m - negativesThisMonth;
            if (remaining < 1m) break;

            var max = (int)Math.Floor(Math.Min(_maxRandomTransactionAmount, remaining));
            if (max < 1) break;
            var merchant = _fakeMerchants[random.Next(_fakeMerchants.Length)];
            if (!randomAmountsByMerchant.TryGetValue(merchant.Description, out var usedAmounts))
                randomAmountsByMerchant[merchant.Description] = usedAmounts = [];

            if (!TryPickDistinctAmount(max, usedAmounts, random, out var amount)) continue;
            usedAmounts.Add(amount);

            var labels = LabelsFor(labelByName, merchant.LabelName);
            add(date, -amount, merchant.Description, labels);
            negativesThisMonth += amount;
        }
        return negativesThisMonth;
    }

    // Random spend is meant to be one-off. Two charges at the same merchant with near-equal amounts
    // (within the recurring detector's tolerance) can coincidentally look like a quarterly or annual
    // subscription and show up as duplicate "Tram pass"-style rows on the Subscriptions page (#904),
    // so every merchant's random amounts are kept clearly apart from each other.
    private static bool TryPickDistinctAmount(int max, List<int> usedAmounts, Random random, out int amount)
    {
        for (var attempt = 0; attempt < _maxAmountPickAttempts; attempt++)
        {
            var candidate = random.Next(1, max + 1);
            if (usedAmounts.Any(used => AreTooSimilar(used, candidate))) continue;

            amount = candidate;
            return true;
        }

        amount = 0;
        return false;
    }

    private static bool AreTooSimilar(int a, int b) =>
        Math.Abs(a - b) <= Math.Max(a, b) * _distinctAmountGap;

    private static List<FinancialLabel> LabelsFor(Dictionary<string, FinancialLabel> labelByName, string name) =>
        labelByName.TryGetValue(name, out var label) ? [label] : [];

    private async Task<List<FinancialLabel>> GetSeedableLabels(CancellationToken cancellationToken)
    {
        var all = await financialLabelsRepository.GetLabels(cancellationToken).ToListAsync(cancellationToken);
        // The NoMatch sentinel is reserved for the AI label setter to mark "no real category fit" — it must never be seeded onto demo data.
        return all.Where(l => !string.Equals(l.Name, WellKnownFinancialLabels.NoMatch, StringComparison.Ordinal)).ToList();
    }
}