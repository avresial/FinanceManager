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
    // Income labels can only appear on entries whose value change is positive (money coming in).
    private static readonly HashSet<string> _incomeLabels = new(StringComparer.OrdinalIgnoreCase)
    {
        "Salary",
        "Investment",
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
        "Undisclosed Expense",
    };

    public async Task Seed(int userId, DateTime start, DateTime end, CancellationToken cancellationToken = default)
    {
        if (await currencyAccountRepository.GetAvailableAccounts(userId).AnyAsync(cancellationToken)) return;

        var labels = await GetSeedableLabels(cancellationToken);

        logger.LogTrace("Seeding cash currency account.");
        await SeedCashAccount(userId, labels, start, end, cancellationToken);

        logger.LogTrace("Seeding loan currency account.");
        await SeedLoanAccount(userId, labels, start, end, cancellationToken);
    }

    private async Task SeedCashAccount(int userId, IReadOnlyList<FinancialLabel> labels, DateTime start, DateTime end, CancellationToken cancellationToken)
    {
        var account = new CurrencyAccount(userId, 0, $"{AccountLabel.Cash} 1", AccountLabel.Cash);

        // Opening balance keeps the account in the black throughout the seeded window even with daily expenses.
        account.AddEntry(new AddCurrencyEntryDto(start, 2_000, "Opening balance", null, []), false);

        for (var date = start.AddDays(1); date <= end; date = date.AddDays(1))
        {
            cancellationToken.ThrowIfCancellationRequested();
            // Monthly salary on the 1st.
            if (date.Day == 1)
            {
                var salary = Random.Shared.Next(3_000, 5_000);
                account.AddEntry(new AddCurrencyEntryDto(date, salary, "Monthly salary", null, PickLabels(labels, salary)), false);
            }

            // Most days have a small expense; a minority have a small positive bump (refund / freelance).
            var change = Random.Shared.Next(0, 100) < 80
                ? -Random.Shared.Next(5, 120)
                : Random.Shared.Next(20, 300);

            account.AddEntry(new AddCurrencyEntryDto(date, change, "", null, PickLabels(labels, change)), false);
        }

        account.RecalculateEntryValues(account.Entries.Count - 1);
        await accountRepository.AddAccount(account);
    }

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
