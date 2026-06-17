using FinanceManager.Domain.Entities.MoneyFlowModels;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.FinancialAccounts.Shared.Repositories;
using FinanceManager.Domain.FinancialAccounts.Shared.Services;
using FinanceManager.Domain.Identity.Services;
using FinanceManager.Domain.Labels.Entities;
using FinanceManager.Domain.Labels.Services;

namespace FinanceManager.Application.Labels.RecurringTransactions;

public class RecurringTransactionDetectorService(IFinancialAccountRepository financialAccountRepository) : IRecurringTransactionDetectorService
{
    private const decimal _minimumMonthlyAverage = 100m;
    private const double _similarityThreshold = 0.75;

    public async Task<List<RecurringTransactionResult>> GetRecurringTransactions(int userId, CancellationToken cancellationToken = default)
    {
        var currentMonthStart = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);

        var months = Enumerable.Range(1, 3)
            .Select(i => currentMonthStart.AddMonths(-i))
            .ToList();

        var start = months.Min();
        var end = currentMonthStart.AddTicks(-1);

        var rawEntries = new List<(string Merchant, int MonthIndex, decimal Amount, int AccountId, int EntryId)>();

        await foreach (var account in financialAccountRepository.GetAccounts<CurrencyAccount>(userId, start, end))
        {
            if (account?.Entries is null) continue;

            foreach (var entry in account.Entries)
            {
                if (entry.ValueChange >= 0) continue;
                if (entry.PostingDate < start || entry.PostingDate >= currentMonthStart) continue;

                var merchantName = !string.IsNullOrEmpty(entry.ContractorDetails)
                    ? entry.ContractorDetails
                    : entry.Description;

                if (string.IsNullOrEmpty(merchantName)) continue;

                var monthIndex = months.FindIndex(m => m.Year == entry.PostingDate.Year && m.Month == entry.PostingDate.Month);
                if (monthIndex < 0) continue;

                rawEntries.Add((merchantName, monthIndex, Math.Abs(entry.ValueChange), account.AccountId, entry.EntryId));
            }
        }

        var clusterRepresentatives = new List<string>();
        var clusterMap = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var (merchant, _, _, _, _) in rawEntries)
        {
            if (clusterMap.ContainsKey(merchant)) continue;

            var matched = false;
            foreach (var rep in clusterRepresentatives)
            {
                if (CalculateSimilarity(rep, merchant) >= _similarityThreshold)
                {
                    clusterMap[merchant] = rep;
                    matched = true;
                    break;
                }
            }

            if (!matched)
            {
                clusterRepresentatives.Add(merchant);
                clusterMap[merchant] = merchant;
            }
        }

        var merchantMonthSpend = new Dictionary<string, Dictionary<int, decimal>>();
        var clusterEntries = new Dictionary<string, List<RecurringTransactionEntryReference>>();

        foreach (var (merchant, monthIndex, amount, accountId, entryId) in rawEntries)
        {
            var cluster = clusterMap[merchant];

            if (!merchantMonthSpend.TryGetValue(cluster, out var byMonth))
            {
                byMonth = [];
                merchantMonthSpend[cluster] = byMonth;
            }

            byMonth[monthIndex] = byMonth.GetValueOrDefault(monthIndex) + amount;

            if (!clusterEntries.TryGetValue(cluster, out var entries))
            {
                entries = [];
                clusterEntries[cluster] = entries;
            }

            entries.Add(new RecurringTransactionEntryReference(accountId, entryId));
        }

        return merchantMonthSpend
            .Where(kv => kv.Value.Count == 3)
            .Select(kv => new RecurringTransactionResult
            {
                Name = kv.Key,
                Value = Math.Round(kv.Value.Values.Average(), 2),
                Entries = clusterEntries.GetValueOrDefault(kv.Key, [])
            })
            .Where(r => r.Value >= _minimumMonthlyAverage)
            .OrderByDescending(r => r.Value)
            .ToList();
    }

    private static double CalculateSimilarity(string a, string b)
    {
        if (string.Equals(a, b, StringComparison.OrdinalIgnoreCase)) return 1.0;
        if (a.Length == 0 || b.Length == 0) return 0.0;

        var aLower = a.ToLowerInvariant();
        var bLower = b.ToLowerInvariant();
        var distance = LevenshteinDistance(aLower, bLower);
        return 1.0 - (double)distance / Math.Max(aLower.Length, bLower.Length);
    }

    private static int LevenshteinDistance(string s, string t)
    {
        int m = s.Length, n = t.Length;
        var d = new int[m + 1, n + 1];

        for (int i = 0; i <= m; i++) d[i, 0] = i;
        for (int j = 0; j <= n; j++) d[0, j] = j;

        for (int j = 1; j <= n; j++)
            for (int i = 1; i <= m; i++)
                d[i, j] = s[i - 1] == t[j - 1]
                    ? d[i - 1, j - 1]
                    : 1 + Math.Min(d[i - 1, j], Math.Min(d[i, j - 1], d[i - 1, j - 1]));

        return d[m, n];
    }
}