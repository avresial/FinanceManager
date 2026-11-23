using FinanceManager.Application.Dashboard;
using FinanceManager.Domain.FinancialAccounts.Shared.Services;
using FinanceManager.Domain.Identity.Services;
using FinanceManager.Domain.Labels.Repositories;
using FinanceManager.Domain.MoneyFlow.Entities;
using FinanceManager.Domain.MoneyFlow.Services;
using FinanceManager.Domain.Transactions.Cash.Repositories;

namespace FinanceManager.Application.MoneyFlow.LabelsValue;

public class LabelsValueService(ICashTransactionReader cashTransactionReader, IFinancialLabelsRepository financialLabelsRepository) : ILabelsValueService
{
    public async Task<List<NameValueResult>> GetLabelsValue(int userId, DateTime start, DateTime end)
    {
        end = DashboardReadCutoff.Clamp(end, DateTime.UtcNow);

        var labels = await financialLabelsRepository.GetLabels().ToListAsync();
        var totals = await cashTransactionReader.GetLabelTotals(userId, start, end);

        // TODO: Add labels for stock accounts?
        return [.. labels.Select(label => new NameValueResult { Name = label.Name, Value = totals.GetValueOrDefault(label.Id) }).OrderByDescending(x => x.Value)];
    }
}