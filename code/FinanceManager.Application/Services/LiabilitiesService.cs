using FinanceManager.Domain.Entities.Accounts;
using FinanceManager.Domain.Entities.MoneyFlowModels;
using FinanceManager.Domain.Enums;
using FinanceManager.Domain.Repositories.Account;
using FinanceManager.Domain.Services;

namespace FinanceManager.Application.Services;
public class LiabilitiesService(IFinancalAccountRepository bankAccountRepository) : ILiabilitiesService
{
    private readonly IFinancalAccountRepository _financialAccountService = bankAccountRepository;

    public Task<List<PieChartModel>> GetEndLiabilitiesPerAccount(int userId, DateTime start, DateTime end)
    {
        throw new NotImplementedException();
    }

    public Task<List<PieChartModel>> GetEndLiabilitiesPerType(int userId, DateTime start, DateTime end)
    {
        throw new NotImplementedException();
    }

    public Task<List<TimeSeriesModel>> GetLiabilitiesTimeSeries(int userId, DateTime start, DateTime end)
    {
        throw new NotImplementedException();
    }

    public Task<List<TimeSeriesModel>> GetLiabilitiesTimeSeries(int userId, DateTime start, DateTime end, InvestmentType investmentType)
    {
        throw new NotImplementedException();
    }


    public async Task<bool> IsAnyAccountWithLiabilities(int userId)
    {
        var BankAccounts = _financialAccountService.GetAccounts<BankAccount>(userId, DateTime.UtcNow.AddDays(-1), DateTime.UtcNow).ToList();
        foreach (var bankAccount in BankAccounts)
        {
            if (bankAccount.Entries is not null && bankAccount.Entries.Count > 0)
            {
                var youngestEntry = bankAccount.Entries.FirstOrDefault();
                if (youngestEntry is not null && youngestEntry.Value < 0)
                    return true;
            }
            else if (bankAccount.OlderThenLoadedEntry is not null)
            {
                var newBankAccount = _financialAccountService.GetAccount<BankAccount>(userId, bankAccount.AccountId, bankAccount.OlderThenLoadedEntry.Value, bankAccount.OlderThenLoadedEntry.Value.AddSeconds(1));
                if (newBankAccount is null || newBankAccount.Entries is null) continue;
                var youngestEntry = newBankAccount.Entries.FirstOrDefault();
                if (youngestEntry is not null && youngestEntry.Value < 0)
                    return true;
            }
        }

        return await Task.FromResult(false);
    }
}
