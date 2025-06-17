using FinanceManager.Domain.Entities.Accounts;
using FinanceManager.Domain.Entities.Accounts.Entries;
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
        foreach (BankAccount account in await _financialAccountService.GetAccounts<BankAccount>(userId, start, end))
        {
            if (account is null || account.Entries is null) return result;
            BankAccountEntry? entry = account.Entries.FirstOrDefault();

            if (entry is null)
            {
                if (account.NextOlderEntry is null) continue;

                entry = account.NextOlderEntry;
            }

            if (entry.Value > 0) continue;

            result.Add(new PieChartModel()
            {
                Name = account.Name,
                Value = entry.Value
            });
        }

        return await Task.FromResult(result);
    }
    public async Task<List<PieChartModel>> GetEndLiabilitiesPerType(int userId, DateTime start, DateTime end)
    {
        List<PieChartModel> result = [];
        foreach (BankAccount account in await _financialAccountService.GetAccounts<BankAccount>(userId, start, end))
        {
            if (account is null || account.Entries is null) return result;
            BankAccountEntry? entry = account.Entries.FirstOrDefault();

            if (entry is null)
            {
                if (account.NextOlderEntry is null) continue;

                entry = account.NextOlderEntry;
            }

            if (entry.Value > 0) continue;

            var existingResult = result.FirstOrDefault(x => x.Name == account.AccountType.ToString());
            if (existingResult is null)
            {
                result.Add(new PieChartModel()
                {
                    Name = account.AccountType.ToString(),
                    Value = entry.Value
                });
            }
            else
            {
                existingResult.Value += entry.Value;
            }
        }

        return await Task.FromResult(result);
    }
    public async Task<List<TimeSeriesModel>> GetLiabilitiesTimeSeries(int userId, DateTime start, DateTime end)
    {
        if (start == new DateTime()) return [];

        Dictionary<DateTime, decimal> prices = [];
        TimeSpan step = new TimeSpan(1, 0, 0, 0);

        foreach (BankAccount account in await _financialAccountService.GetAccounts<BankAccount>(userId, start, end))
        {
            if (account is null || account.Entries is null) continue;

            decimal previousValue = 0;

            if (account.Entries.Any() && account.Entries.Last().PostingDate.Date == start.Date)
            {
                previousValue = account.Entries.Last().Value - account.Entries.Last().ValueChange;
            }
            else if (account.NextOlderEntry is not null)
            {
                previousValue = account.NextOlderEntry.Value;
            }

            if (previousValue > 0) continue;


            for (DateTime date = start; date <= end; date = date.Add(step))
            {
                if (!prices.ContainsKey(date)) prices.Add(date, 0);

                var newestEntry = account.Get(date).OrderByDescending(x => x.PostingDate).FirstOrDefault();
                if (newestEntry is null)
                {
                    prices[date] += previousValue;
                    continue;
                }

                if (newestEntry.Value > 0) break;

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
            else if (bankAccount.NextOlderEntry is not null && bankAccount.NextOlderEntry.Value < 0)
            {
                return true;
            }
        }

        return await Task.FromResult(false);
    }
}
