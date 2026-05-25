using FinanceManager.Domain.Entities.FinancialAccounts.Currencies;
using FinanceManager.Domain.Entities.Shared.Accounts;
using FinanceManager.Domain.Enums;
using FinanceManager.Domain.Repositories;
using FinanceManager.Domain.Repositories.Account;

namespace FinanceManager.Application.Services.Seeders;

internal static class CurrencyAccountSeeder
{
    private const string SalaryLabelName = "Salary";

    internal static async Task AddAccount(this IFinancialAccountRepository accountRepository, int userId, AccountLabel accountLabel, List<FinancialLabel> labels,
        DateTime start, DateTime end)
    {
        CurrencyAccount newAccount = new(userId, 0, $"{accountLabel} 1", accountLabel);
        var days = (int)(end - start).TotalDays;
        if (accountLabel == AccountLabel.Loan)
        {
            var openingChange = Random.Shared.Next(days * -100, days * -100);
            newAccount.AddEntry(new AddCurrencyEntryDto(start, openingChange, "", null, PickEntryLabels(labels, openingChange)), false);
            for (DateTime date = start.AddDays(1); date <= end; date = date.AddDays(1))
            {
                var change = Random.Shared.Next(10, 100);
                newAccount.AddEntry(new AddCurrencyEntryDto(date, change, "", null, PickEntryLabels(labels, change)), false);
            }
        }
        else
        {
            for (var date = start; date <= end; date = date.AddDays(1))
            {
                var change = Random.Shared.Next(-90, 100);
                newAccount.AddEntry(new AddCurrencyEntryDto(date, change, "", null, PickEntryLabels(labels, change)), false);
            }
        }
        newAccount.RecalculateEntryValues(newAccount.Entries.Count - 1);
        await accountRepository.AddAccount(newAccount);
    }

    internal static async Task<List<FinancialLabel>> GetSeedableLabels(this IFinancialLabelsRepository labelsRepository)
    {
        // Exclude the NoMatch sentinel — it's reserved for the AI label setter to mark "no real category fit".
        var all = await labelsRepository.GetLabels().ToListAsync();
        return all.Where(l => !string.Equals(l.Name, WellKnownFinancialLabels.NoMatch, StringComparison.Ordinal)).ToList();
    }

    private static List<FinancialLabel> PickEntryLabels(IReadOnlyList<FinancialLabel> pool, decimal valueChange)
    {
        var candidates = valueChange < 0
            ? pool.Where(l => !string.Equals(l.Name, SalaryLabelName, StringComparison.OrdinalIgnoreCase)).ToList()
            : pool.ToList();
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
}