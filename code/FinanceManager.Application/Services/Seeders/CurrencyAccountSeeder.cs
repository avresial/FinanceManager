using FinanceManager.Domain.Entities.FinancialAccounts.Currencies;
using FinanceManager.Domain.Entities.Shared.Accounts;
using FinanceManager.Domain.Enums;
using FinanceManager.Domain.Repositories;
using FinanceManager.Domain.Repositories.Account;

namespace FinanceManager.Application.Services.Seeders;

internal static class CurrencyAccountSeeder
{
    internal static async Task AddAccount(this IFinancialAccountRepository accountRepository, int userId, AccountLabel accountLabel, List<FinancialLabel> labels,
        DateTime start, DateTime end)
    {
        CurrencyAccount newAccount = new(userId, 0, $"{accountLabel} 1", accountLabel);
        var days = (int)(end - start).TotalDays;
        if (accountLabel == AccountLabel.Loan)
        {
            newAccount.AddEntry(new AddCurrencyEntryDto(start, Random.Shared.Next(days * -100, days * -100), "", null, labels), false);
            for (DateTime date = start.AddDays(1); date <= end; date = date.AddDays(1))
                newAccount.AddEntry(new AddCurrencyEntryDto(date, Random.Shared.Next(10, 100), "", null, labels), false);
        }
        else
        {
            for (var date = start; date <= end; date = date.AddDays(1))
                newAccount.AddEntry(new AddCurrencyEntryDto(date, Random.Shared.Next(-90, 100), "", null, labels), false);
        }
        newAccount.RecalculateEntryValues(newAccount.Entries.Count - 1);
        await accountRepository.AddAccount(newAccount);
    }
    internal static async IAsyncEnumerable<FinancialLabel> GetRandomLabels(this IFinancialLabelsRepository accountRepository)
    {
        await foreach (var label in accountRepository.GetLabels().Where(x => Random.Shared.Next(0, 100) < 40))
            yield return label;
    }
}