using FinanceManager.Domain.Entities.Bonds;
using FinanceManager.Domain.Repositories.Account;

namespace FinanceManager.Application.Services.Seeders;

internal static class BondAccountSeeder
{
    internal static async Task AddBondAccount(this IFinancialAccountRepository accountRepository, int userId, DateTime start, DateTime end)
    {
        BondAccount newAccount = new(userId, 0, "Bond 1");
        int bondId = 1;

        for (var date = start; date <= end; date = date.AddDays(1))
            newAccount.Add(new BondAccountEntry(newAccount.AccountId, 0, date, 0, Random.Shared.Next(-90, 100), bondId), false);

        newAccount.RecalculateEntryValues(newAccount.Entries.Count - 1);
        await accountRepository.AddAccount(newAccount);
    }
}