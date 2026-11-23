using FinanceManager.Application.Shared;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.FinancialAccounts.Shared.Entities;
using FinanceManager.Domain.FinancialAccounts.Shared.Services;
using FinanceManager.Domain.Identity.Services;
using FinanceManager.Domain.Labels.Repositories;
using FinanceManager.Domain.MoneyFlow.Entities;
using FinanceManager.Domain.MoneyFlow.Services;
using FinanceManager.Domain.Transactions.Cash.Repositories;

namespace FinanceManager.Application.MoneyFlow.Spending;

public class CurrencyEssentialSpendingService(ICashTransactionReader cashTransactionReader, IFinancialLabelsRepository financialLabelsRepository) : IEssentialSpendingServiceTyped
{
    public async Task<List<TimeSeriesModel>> GetEssentialSpending(int userId, Currency currency, DateTime start, DateTime end)
    {
        if (end > DateTime.UtcNow) end = DateTime.UtcNow;

        var entries = await cashTransactionReader.GetLabeledEntries(userId, start, end, endInclusive: true);
        if (entries.Count == 0 || end.Date < start.Date) return [];

        var labelsById = (await financialLabelsRepository.GetLabels().ToListAsync()).ToDictionary(label => label.Id);

        // Every day of the window gets a bucket (zero when nothing essential was spent), newest first like the other money-flow series.
        Dictionary<DateTime, decimal> result = [];
        for (var date = end.Date; date >= start.Date; date = date.AddDays(-1))
            result[date] = 0;

        foreach (var entry in entries)
        {
            if (entry.ValueChange >= 0) continue;
            var classification = FinancialLabelClassificationCatalog.ResolveClassification(entry.LabelIds.Select(id => labelsById.GetValueOrDefault(id)).OfType<FinancialLabel>());
            if (classification != FinancialLabelClassificationCatalog.EssentialValue) continue;

            result[entry.PostingDate.Date] += entry.ValueChange;
        }

        return BucketToSeries(result);
    }

    private static List<TimeSeriesModel> BucketToSeries(Dictionary<DateTime, decimal> data) =>
        TimeBucketService.Get(data.Select(x => (x.Key, x.Value)))
            .Select(bucket => new TimeSeriesModel(bucket.Date, bucket.Objects.Sum()))
            .ToList();
}