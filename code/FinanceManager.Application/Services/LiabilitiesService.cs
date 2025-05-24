using FinanceManager.Domain.Entities.Accounts;
using FinanceManager.Domain.Entities.MoneyFlowModels;
using FinanceManager.Domain.Repositories.Account;
using FinanceManager.Domain.Services;

namespace FinanceManager.Application.Services;

public class LiabilitiesService(IFinancalAccountRepository bankAccountRepository) : ILiabilitiesService
{
    private readonly IFinancalAccountRepository _financialAccountService = bankAccountRepository;

    public async Task<List<PieChartModel>> GetEndLiabilitiesPerAccount(int userId, DateTime start, DateTime end)
    {
        List<PieChartModel> result = [];
        var BankAccounts = await _financialAccountService.GetAccounts<BankAccount>(userId, start, end);
        foreach (BankAccount account in BankAccounts.Where(x => x.Entries is not null && x.Entries.Count != 0 && x.Entries.First().Value <= 0))
        {
            if (account is null || account.Entries is null) return result;

            result.Add(new PieChartModel()
            {
                Name = account.Name,
                Value = account.Entries.First().Value
            });
        }


        return await Task.FromResult(result);
    }
    public async Task<List<PieChartModel>> GetEndLiabilitiesPerType(int userId, DateTime start, DateTime end)
    {
        List<PieChartModel> result = [];
        var BankAccounts = await _financialAccountService.GetAccounts<BankAccount>(userId, start, end);
        foreach (BankAccount account in BankAccounts.Where(x => x.Entries is not null && x.Entries.Count != 0 && x.Entries.First().Value <= 0))
        {
            if (account is null || account.Entries is null) return result;
            var existingResult = result.FirstOrDefault(x => x.Name == account.AccountType.ToString());
            if (existingResult is null)
            {
                result.Add(new PieChartModel()
                {
                    Name = account.AccountType.ToString(),
                    Value = account.Entries.First().Value
                });
            }
            else
            {
                existingResult.Value += account.Entries.First().Value;
            }
        }
        return await Task.FromResult(result);
    }
    public async Task<List<TimeSeriesModel>> GetLiabilitiesTimeSeries(int userId, DateTime start, DateTime end)
    {
        if (start == new DateTime()) return [];

        Dictionary<DateTime, decimal> prices = [];
        TimeSpan step = new TimeSpan(1, 0, 0, 0);

        var BankAccounts = await _financialAccountService.GetAccounts<BankAccount>(userId, start, end);
        foreach (BankAccount account in BankAccounts.Where(x => x.Entries is not null && x.Entries.Count != 0 && x.Entries.First().Value <= 0))
        {
            if (account is null || account.Entries is null) continue;
            decimal previousValue = account.Entries.Last().Value - account.Entries.Last().ValueChange;

            for (DateTime date = start; date <= end; date = date.Add(step))
            {
                if (!prices.ContainsKey(date)) prices.Add(date, 0);

                var newestEntry = account.Get(date).OrderByDescending(x => x.PostingDate).FirstOrDefault();
                if (newestEntry is null)
                {
                    prices[date] += previousValue;
                    continue;
                }

                prices[date] += newestEntry.Value;
                previousValue = prices[date];
            }
        }

        return await Task.FromResult(prices.Select(x => new TimeSeriesModel() { DateTime = x.Key, Value = x.Value })
                    .OrderByDescending(x => x.DateTime)
                    .ToList());
    }
    public async Task<bool> IsAnyAccountWithLiabilities(int userId)
    {
        var BankAccounts = (await _financialAccountService.GetAccounts<BankAccount>(userId, DateTime.UtcNow.AddDays(-1), DateTime.UtcNow)).ToList();
        foreach (var bankAccount in BankAccounts)
        {
            if (bankAccount.Entries is not null && bankAccount.Entries.Count > 0)
            {
                var youngestEntry = bankAccount.Entries.FirstOrDefault();
                if (youngestEntry is not null && youngestEntry.Value < 0)
                    return true;
            }
            else if (bankAccount.OlderThanLoadedEntry is not null)
            {
                var newBankAccount = await _financialAccountService.GetAccount<BankAccount>(userId, bankAccount.AccountId, bankAccount.OlderThanLoadedEntry.Value, bankAccount.OlderThanLoadedEntry.Value.AddSeconds(1));
                if (newBankAccount is null || newBankAccount.Entries is null) continue;
                var youngestEntry = newBankAccount.Entries.FirstOrDefault();
                if (youngestEntry is not null && youngestEntry.Value < 0)
                    return true;
            }
        }

        return await Task.FromResult(false);
    }
}
