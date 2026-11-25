using FinanceManager.Application.Dashboard;
using FinanceManager.Domain.Currencies.Entities;
using FinanceManager.Domain.Identity.Services;
using FinanceManager.Domain.Labels.Entities;
using FinanceManager.Domain.Labels.Repositories;
using FinanceManager.Domain.MoneyFlow.Entities;
using FinanceManager.Domain.MoneyFlow.Services;
using FinanceManager.Domain.Transactions.Cash.Repositories;

namespace FinanceManager.Application.MoneyFlow.Spending;

public class CurrencyExpenseDistributionService(ICashTransactionReader cashTransactionReader, IFinancialLabelsRepository financialLabelsRepository) : IExpenseDistributionServiceTyped
{
    public async Task<List<NameValueResult>> GetExpenseDistribution(int userId, Currency currency, DateTime start, DateTime end)
    {
        end = DashboardReadCutoff.Clamp(end, DateTime.UtcNow);

        var labelsById = (await financialLabelsRepository.GetLabels().ToListAsync()).ToDictionary(label => label.Id);
        Dictionary<string, decimal> totals = [];

        foreach (var entry in await cashTransactionReader.GetLabeledEntries(userId, start, end, endInclusive: true, outflowsOnly: true))
        {
            var category = FinancialLabelClassificationCatalog.ResolveClassification(entry.LabelIds.Select(id => labelsById.GetValueOrDefault(id)).OfType<FinancialLabel>());
            if (category is null) continue;

            totals[category] = totals.GetValueOrDefault(category) + Math.Abs(entry.ValueChange);
        }

        return totals
            .Select(kv => new NameValueResult(kv.Key, Math.Round(kv.Value, 2)))
            .OrderBy(r => r.Name)
            .ToList();
    }
}