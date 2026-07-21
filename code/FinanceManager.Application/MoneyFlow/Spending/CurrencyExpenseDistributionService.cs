using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.FinancialAccounts.Currencies.Extensions;
using FinanceManager.Domain.FinancialAccounts.Shared.Entities;
using FinanceManager.Domain.FinancialAccounts.Shared.Repositories;
using FinanceManager.Domain.FinancialAccounts.Shared.Services;
using FinanceManager.Domain.Identity.Services;
using FinanceManager.Domain.MoneyFlow.Entities;
using FinanceManager.Domain.MoneyFlow.Services;

namespace FinanceManager.Application.MoneyFlow.Spending;

public class CurrencyExpenseDistributionService(IFinancialAccountRepository financialAccountRepository) : IExpenseDistributionServiceTyped
{
    public async Task<List<NameValueResult>> GetExpenseDistribution(int userId, Currency currency, DateTime start, DateTime end)
    {
        if (end > DateTime.UtcNow) end = DateTime.UtcNow;

        Dictionary<string, decimal> totals = [];

        await foreach (var account in financialAccountRepository.GetAccounts<CurrencyAccount>(userId, start, end))
        {
            if (account?.Entries is null) continue;

            foreach (var entry in account.Entries)
            {
                if (entry.ValueChange >= 0) continue;
                if (entry.PostingDate < start || entry.PostingDate > end) continue;

                var category = FinancialLabelClassificationCatalog.ResolveClassification(entry.Labels);
                if (category is null) continue;

                totals[category] = totals.GetValueOrDefault(category) + Math.Abs(entry.ValueChange);
            }
        }

        return totals
            .Select(kv => new NameValueResult(kv.Key, Math.Round(kv.Value, 2)))
            .OrderBy(r => r.Name)
            .ToList();
    }
}