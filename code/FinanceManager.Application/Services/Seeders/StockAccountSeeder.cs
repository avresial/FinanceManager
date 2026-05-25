using FinanceManager.Domain.Entities.Stocks;
using FinanceManager.Domain.Enums;
using FinanceManager.Domain.Repositories.Account;

namespace FinanceManager.Application.Services.Seeders;

internal static class StockAccountSeeder
{
    internal static async Task AddStockAccount(this IFinancialAccountRepository accountRepository, int userId, DateTime start, DateTime end)
    {
        StockAccount newAccount = new(userId, 0, "Stock 1");

        for (var date = start; date <= end; date = date.AddDays(1))
            newAccount.Add(GetNewStockAccountEntry(userId, 0, date, -90, 100, "CSPX.LON"), false);
        newAccount.RecalculateEntryValues(newAccount.Entries.Count - 1);
        await accountRepository.AddAccount(newAccount);
    }


    public static StockAccountEntry GetNewStockAccountEntry(int accountId, int entryId, DateTime date, int minValue, int maxValue,
        string ticker, InvestmentType investmentType = InvestmentType.Stock) =>
         new(accountId, entryId, date, 0, Random.Shared.Next(minValue, maxValue), ticker, investmentType);
}