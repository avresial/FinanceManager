using FinanceManager.Domain.Entities.FinancialAccounts.Currencies;
using FinanceManager.Domain.Entities.MoneyFlowModels;
using FinanceManager.Domain.Identity.Repositories;
using FinanceManager.Domain.Identity.Services;
using FinanceManager.Domain.Labels.Repositories;
using FinanceManager.Domain.MoneyFlow.Services;
using FinanceManager.Domain.Repositories;
using FinanceManager.Domain.Repositories.Account;
using FinanceManager.Domain.Services;

namespace FinanceManager.Application.MoneyFlow.LabelsValue;

public class LabelsValueService(IFinancialAccountRepository financialAccountRepository, IFinancialLabelsRepository financialLabelsRepository) : ILabelsValueService
{
    public async Task<List<NameValueResult>> GetLabelsValue(int userId, DateTime start, DateTime end)
    {
        if (end > DateTime.UtcNow) end = DateTime.UtcNow;

        var labels = await financialLabelsRepository.GetLabels().ToListAsync();

        var result = labels.ToDictionary(x => x.Id, x => new NameValueResult() { Name = x.Name, Value = 0 });
        await foreach (CurrencyAccount account in financialAccountRepository.GetAccounts<CurrencyAccount>(userId, start, end))
        {
            if (account is null || account.Entries is null) continue;
            if (account.Entries is null || !account.Entries.Any()) continue;

            foreach (var entry in account.Entries.Where(x => x.Labels is not null && x.Labels.Any()))
            {
                foreach (var label in entry.Labels)
                {
                    if (!result.ContainsKey(label.Id)) continue;
                    result[label.Id].Value += entry.ValueChange;
                }
            }
        }

        // TODO: Add labels for stock accounts?

        return result.Values.OrderByDescending(x => x.Value).ToList();
    }
}