using FinanceManager.Domain.Entities.FinancialAccounts.Currencies;
using FinanceManager.Domain.Entities.MoneyFlowModels;
using FinanceManager.Domain.Repositories.Account;
using FinanceManager.Domain.Services;

namespace FinanceManager.Application.Services;

public class RecurringTransactionDetectorService(IFinancialAccountRepository financialAccountRepository) : IRecurringTransactionDetectorService
{
    private const decimal MinimumMonthlyAverage = 100m;

    public async Task<List<NameValueResult>> GetRecurringTransactions(int userId, CancellationToken cancellationToken = default)
    {
        var currentMonthStart = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);

        var months = Enumerable.Range(1, 3)
            .Select(i => currentMonthStart.AddMonths(-i))
            .ToList();

        var start = months.Min();
        var end = currentMonthStart.AddDays(-1);

        var merchantMonthSpend = new Dictionary<string, Dictionary<int, decimal>>();

        await foreach (var account in financialAccountRepository.GetAccounts<CurrencyAccount>(userId, start, end))
        {
            if (account?.Entries is null) continue;

            foreach (var entry in account.Entries)
            {
                if (entry.ValueChange >= 0) continue;
                if (entry.PostingDate < start || entry.PostingDate > end) continue;

                var merchantName = !string.IsNullOrEmpty(entry.ContractorDetails)
                    ? entry.ContractorDetails
                    : entry.Description;

                if (string.IsNullOrEmpty(merchantName)) continue;

                var monthIndex = months.FindIndex(m => m.Year == entry.PostingDate.Year && m.Month == entry.PostingDate.Month);
                if (monthIndex < 0) continue;

                if (!merchantMonthSpend.TryGetValue(merchantName, out var byMonth))
                {
                    byMonth = [];
                    merchantMonthSpend[merchantName] = byMonth;
                }

                byMonth[monthIndex] = byMonth.GetValueOrDefault(monthIndex) + Math.Abs(entry.ValueChange);
            }
        }

        return merchantMonthSpend
            .Where(kv => kv.Value.Count == 3)
            .Select(kv => new NameValueResult(kv.Key, Math.Round(kv.Value.Values.Average(), 2)))
            .Where(r => r.Value >= MinimumMonthlyAverage)
            .OrderByDescending(r => r.Value)
            .ToList();
    }
}
