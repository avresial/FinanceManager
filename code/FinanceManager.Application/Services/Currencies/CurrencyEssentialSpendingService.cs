using FinanceManager.Domain.Entities.Currencies;
using FinanceManager.Domain.Entities.FinancialAccounts.Currencies;
using FinanceManager.Domain.Entities.MoneyFlowModels;
using FinanceManager.Domain.Entities.Shared.Accounts;
using FinanceManager.Domain.Repositories.Account;
using FinanceManager.Domain.Services;

namespace FinanceManager.Application.Services.Currencies;

public class CurrencyEssentialSpendingService(IFinancialAccountRepository financialAccountRepository) : IEssentialSpendingServiceTyped
{
    private static readonly TimeSpan OneDay = TimeSpan.FromDays(1);

    public bool IsOfType<T>() => typeof(T) == typeof(CurrencyAccount);

    public Task<List<TimeSeriesModel>> GetEssentialSpending(int userId, Currency currency, DateTime start, DateTime end) =>
        GetEssentialSpending(userId, currency, start, end, []);

    public async Task<List<TimeSeriesModel>> GetEssentialSpending(int userId, Currency currency, DateTime start, DateTime end, IReadOnlyCollection<int> accountIds)
    {
        var result = await AggregateByDay(userId, start, end, accountIds);
        return BucketToSeries(result);
    }

    private async Task<Dictionary<DateTime, decimal>> AggregateByDay(int userId, DateTime start, DateTime end, IReadOnlyCollection<int> accountIds)
    {
        if (end > DateTime.UtcNow) end = DateTime.UtcNow;

        Dictionary<DateTime, decimal> result = [];
        var accountIdFilter = accountIds.Count > 0 ? accountIds.ToHashSet() : [];

        await foreach (var account in financialAccountRepository.GetAccounts<CurrencyAccount>(userId, start, end))
        {
            if (account?.Entries is null) continue;
            if (accountIdFilter.Count > 0 && !accountIdFilter.Contains(account.AccountId)) continue;

            for (var date = end.Date; date >= start.Date; date = date.Add(-OneDay))
            {
                if (!result.ContainsKey(date)) result.Add(date, 0);

                var entries = account.Get(date);
                foreach (var entry in entries.Select(x => x as FinancialEntryBase).Where(x => x is not null))
                {
                    if (entry!.PostingDate.Date != date.Date) continue;
                    if (entry.ValueChange >= 0) continue;
                    if (!HasResolvedClassification(entry.Labels, FinancialLabelClassificationCatalog.EssentialValue)) continue;

                    result[date] += entry.ValueChange;
                }
            }
        }

        return result;
    }

    private static bool HasResolvedClassification(IEnumerable<FinancialLabel> labels, string expectedValue)
    {
        var counts = labels
            .SelectMany(label => label.Classifications)
            .Where(classification => classification.Kind == FinancialLabelClassificationCatalog.SpendingNecessityKind)
            .GroupBy(classification => classification.Value)
            .Select(group => new { Value = group.Key, Count = group.Count() })
            .OrderByDescending(group => group.Count)
            .ThenBy(group => group.Value, StringComparer.Ordinal)
            .ToList();

        if (counts.Count == 0)
            return false;

        if (counts.Count > 1 && counts[0].Count == counts[1].Count)
            return false;

        return counts[0].Value == expectedValue;
    }

    private static List<TimeSeriesModel> BucketToSeries(Dictionary<DateTime, decimal> data) =>
        TimeBucketService.Get(data.Select(x => (x.Key, x.Value)))
            .Select(bucket => new TimeSeriesModel(bucket.Date, bucket.Objects.Sum()))
            .ToList();
}