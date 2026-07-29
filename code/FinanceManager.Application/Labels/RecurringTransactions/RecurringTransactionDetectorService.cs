using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.FinancialAccounts.Shared.Repositories;
using FinanceManager.Domain.Labels.Commands;
using FinanceManager.Domain.Labels.Entities;
using FinanceManager.Domain.Labels.Repositories;
using FinanceManager.Domain.Labels.Services;
using FinanceManager.Domain.Shared.Services;
using System.Security.Cryptography;
using System.Text;

namespace FinanceManager.Application.Labels.RecurringTransactions;

public class RecurringTransactionDetectorService(
    IFinancialAccountRepository financialAccountRepository,
    IRecurringSubscriptionRepository subscriptionRepository,
    IDateTimeProvider dateTimeProvider) : IRecurringTransactionDetectorService
{
    private const decimal _minimumMonthlyAverage = 5m;
    private const double _similarityThreshold = 0.75;

    public async Task<List<RecurringTransactionResult>> GetRecurringTransactions(
        int userId,
        CancellationToken cancellationToken = default)
    {
        var today = dateTimeProvider.TodayUtc;
        var start = today.AddMonths(-25);
        var end = today.AddDays(1);
        var rawEntries = new List<DetectedEntry>();

        await foreach (var account in financialAccountRepository.GetAccounts<CurrencyAccount>(userId, start, end))
        {
            cancellationToken.ThrowIfCancellationRequested();
            foreach (var entry in account.Entries)
            {
                if (entry.ValueChange >= 0 || entry.PostingDate < start || entry.PostingDate >= end)
                    continue;

                var merchant = string.IsNullOrWhiteSpace(entry.ContractorDetails)
                    ? entry.Description
                    : entry.ContractorDetails;
                if (string.IsNullOrWhiteSpace(merchant)) continue;

                rawEntries.Add(new(
                    merchant.Trim(),
                    entry.PostingDate,
                    Math.Abs(entry.ValueChange),
                    account.AccountId,
                    entry.EntryId));
            }
        }

        var clusters = Cluster(rawEntries);
        var detected = clusters
            .Select(cluster => BuildResult(cluster.Key, cluster.Value, today))
            .OfType<DetectedPattern>()
            .Where(x => x.Result.MonthlyCost >= _minimumMonthlyAverage)
            .ToList();

        var subscriptions = await subscriptionRepository.GetAll(userId, cancellationToken);
        var added = new List<RecurringSubscription>();
        var matchedIds = new HashSet<Guid>();

        foreach (var pattern in detected)
        {
            var subscription = subscriptions
                .Where(x => !matchedIds.Contains(x.Id))
                .Select(x => (Subscription: x, Similarity: CalculateSimilarity(x.MerchantKey, pattern.MerchantKey)))
                .Where(x => x.Similarity >= _similarityThreshold)
                .OrderByDescending(x => x.Similarity)
                .Select(x => x.Subscription)
                .FirstOrDefault();

            if (subscription is null)
            {
                subscription = new RecurringSubscription
                {
                    Id = CreatePatternId(userId, pattern.MerchantKey),
                    UserId = userId,
                    MerchantKey = pattern.MerchantKey,
                    Name = pattern.Result.Name
                };
                subscriptions.Add(subscription);
                added.Add(subscription);
            }
            else
            {
                subscription.MerchantKey = pattern.MerchantKey;
                subscription.Name = pattern.Result.Name;
            }

            matchedIds.Add(subscription.Id);
            pattern.Result.PatternId = subscription.Id;
            pattern.Result.IsMuted = subscription.IsMuted;
            pattern.Result.IsCancelled = subscription.IsCancelled;
            pattern.Result.IsFlaggedForReview = subscription.IsFlaggedForReview;
        }

        await subscriptionRepository.Save(added, cancellationToken);

        return detected
            .Select(x => x.Result)
            .OrderByDescending(x => x.IsFlaggedForReview)
            .ThenBy(x => x.IsMuted || x.IsCancelled)
            .ThenBy(x => x.NextExpectedChargeDate)
            .ToList();
    }

    public Task<bool> UpdateSubscription(
        int userId,
        Guid patternId,
        UpdateRecurringSubscription command,
        CancellationToken cancellationToken = default) =>
        subscriptionRepository.Update(userId, patternId, command, cancellationToken);

    private static Dictionary<string, List<DetectedEntry>> Cluster(IEnumerable<DetectedEntry> entries)
    {
        var result = new Dictionary<string, List<DetectedEntry>>(StringComparer.OrdinalIgnoreCase);

        foreach (var entry in entries.OrderBy(x => x.Date))
        {
            var representative = result.Keys.FirstOrDefault(
                key => CalculateSimilarity(key, entry.Merchant) >= _similarityThreshold);

            if (representative is null)
            {
                result[entry.Merchant] = [entry];
                continue;
            }

            result[representative].Add(entry);
        }

        return result;
    }

    private static DetectedPattern? BuildResult(string name, List<DetectedEntry> entries, DateTime asOf)
    {
        var occurrences = entries
            .GroupBy(x => x.Date.Date)
            .Select(group => new Occurrence(
                group.Key,
                group.Sum(x => x.Amount),
                group.Select(x => new RecurringTransactionEntryReference(x.AccountId, x.EntryId)).ToList()))
            .OrderBy(x => x.Date)
            .ToList();
        if (occurrences.Count < 2) return null;

        var gaps = occurrences.Zip(occurrences.Skip(1), (previous, current) => (current.Date - previous.Date).TotalDays).ToList();
        var cadence = DetectCadence(gaps);
        if (cadence is null || occurrences.Count < MinimumOccurrences(cadence.Value)) return null;

        var last = occurrences[^1];
        if (asOf.Date - last.Date > MaximumAge(cadence.Value)) return null;

        var average = occurrences.Average(x => x.Amount);
        var monthlyCost = Math.Round(cadence.Value switch
        {
            RecurringCadence.Weekly => average * 52m / 12m,
            RecurringCadence.Monthly => average,
            RecurringCadence.Quarterly => average / 3m,
            RecurringCadence.Annual => average / 12m,
            _ => average
        }, 2);
        var previous = occurrences[^2];
        var nextCharge = AddCadence(last.Date, cadence.Value);
        while (nextCharge <= asOf.Date)
            nextCharge = AddCadence(nextCharge, cadence.Value);

        return new(
            NormalizeMerchant(name),
            new RecurringTransactionResult
            {
                Name = name,
                Value = monthlyCost,
                Cadence = cadence.Value,
                LastChargeDate = last.Date,
                NextExpectedChargeDate = nextCharge,
                LastAmount = Math.Round(last.Amount, 2),
                PreviousAmount = Math.Round(previous.Amount, 2),
                MonthlyCost = monthlyCost,
                AnnualCost = monthlyCost * 12m,
                Entries = occurrences.SelectMany(x => x.Entries).ToList()
            });
    }

    private static RecurringCadence? DetectCadence(List<double> gaps)
    {
        var ordered = gaps.Order().ToList();
        var median = ordered[ordered.Count / 2];
        var cadence = median switch
        {
            >= 5 and <= 10 => RecurringCadence.Weekly,
            >= 20 and <= 45 => RecurringCadence.Monthly,
            >= 70 and <= 110 => RecurringCadence.Quarterly,
            >= 330 and <= 400 => RecurringCadence.Annual,
            _ => (RecurringCadence?)null
        };
        if (cadence is null) return null;

        var regularGaps = gaps.Count(gap => cadence.Value switch
        {
            RecurringCadence.Weekly => gap is >= 5 and <= 10,
            RecurringCadence.Monthly => gap is >= 20 and <= 45,
            RecurringCadence.Quarterly => gap is >= 70 and <= 110,
            RecurringCadence.Annual => gap is >= 330 and <= 400,
            _ => false
        });

        return regularGaps * 10 >= gaps.Count * 7 ? cadence : null;
    }

    private static int MinimumOccurrences(RecurringCadence cadence) =>
        cadence is RecurringCadence.Weekly ? 4 : cadence is RecurringCadence.Monthly ? 3 : 2;

    private static DateTime AddCadence(DateTime date, RecurringCadence cadence) =>
        cadence switch
        {
            RecurringCadence.Weekly => date.AddDays(7),
            RecurringCadence.Monthly => date.AddMonths(1),
            RecurringCadence.Quarterly => date.AddMonths(3),
            RecurringCadence.Annual => date.AddYears(1),
            _ => date
        };

    private static TimeSpan MaximumAge(RecurringCadence cadence) =>
        cadence switch
        {
            RecurringCadence.Weekly => TimeSpan.FromDays(21),
            RecurringCadence.Monthly => TimeSpan.FromDays(60),
            RecurringCadence.Quarterly => TimeSpan.FromDays(150),
            RecurringCadence.Annual => TimeSpan.FromDays(450),
            _ => TimeSpan.Zero
        };

    private static string NormalizeMerchant(string value) =>
        string.Concat(value.Where(char.IsLetterOrDigit)).ToLowerInvariant();

    private static Guid CreatePatternId(int userId, string merchantKey)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes($"{userId}:{merchantKey}"));
        return new Guid(hash.AsSpan(0, 16));
    }

    private static double CalculateSimilarity(string a, string b)
    {
        a = NormalizeMerchant(a);
        b = NormalizeMerchant(b);
        if (string.Equals(a, b, StringComparison.Ordinal)) return 1.0;
        if (a.Length == 0 || b.Length == 0) return 0.0;

        var distance = LevenshteinDistance(a, b);
        return 1.0 - (double)distance / Math.Max(a.Length, b.Length);
    }

    private static int LevenshteinDistance(string s, string t)
    {
        var d = new int[s.Length + 1, t.Length + 1];
        for (var i = 0; i <= s.Length; i++) d[i, 0] = i;
        for (var j = 0; j <= t.Length; j++) d[0, j] = j;

        for (var j = 1; j <= t.Length; j++)
            for (var i = 1; i <= s.Length; i++)
                d[i, j] = s[i - 1] == t[j - 1]
                    ? d[i - 1, j - 1]
                    : 1 + Math.Min(d[i - 1, j], Math.Min(d[i, j - 1], d[i - 1, j - 1]));

        return d[s.Length, t.Length];
    }

    private sealed record DetectedEntry(string Merchant, DateTime Date, decimal Amount, int AccountId, int EntryId);
    private sealed record Occurrence(DateTime Date, decimal Amount, List<RecurringTransactionEntryReference> Entries);
    private sealed record DetectedPattern(string MerchantKey, RecurringTransactionResult Result);
}