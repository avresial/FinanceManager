using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.FinancialAccounts.Shared.Entities;
using FinanceManager.Domain.FinancialAccounts.Shared.Repositories;
using FinanceManager.Domain.Labels.Commands;
using FinanceManager.Domain.Labels.Entities;
using FinanceManager.Domain.Labels.Repositories;
using FinanceManager.Domain.Labels.Services;
using FinanceManager.Domain.MoneyFlow.Entities;
using FinanceManager.Domain.Shared.Services;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace FinanceManager.Application.Labels.RecurringTransactions;

public class RecurringTransactionDetectorService(
    IFinancialAccountRepository financialAccountRepository,
    IRecurringSubscriptionRepository subscriptionRepository,
    IDateTimeProvider dateTimeProvider) : IRecurringTransactionDetectorService
{
    public async Task<List<RecurringTransactionResult>> GetRecurringTransactions(
        int userId,
        CancellationToken cancellationToken = default)
    {
        var asOfDate = dateTimeProvider.TodayUtc;
        var expenses = await GetRecurringTransactionsCore(
            userId,
            asOfDate,
            cancellationToken,
            persistSubscriptions: true,
            includeAccount: null);
        var incomes = await DetectPatterns(
            userId,
            asOfDate,
            static entry => entry.ValueChange > 0,
            isIncome: true,
            static account => account.AccountType == AccountLabel.Cash,
            cancellationToken);

        return expenses
            .Concat(incomes.Select(x => x.Result))
            .OrderByDescending(x => x.IsFlaggedForReview)
            .ThenBy(x => x.IsMuted || x.IsCancelled)
            .ThenBy(x => x.NextExpectedChargeDate)
            .ToList();
    }

    private async Task<List<RecurringTransactionResult>> GetRecurringTransactionsCore(
        int userId,
        DateTime asOfDate,
        CancellationToken cancellationToken,
        bool persistSubscriptions,
        Func<CurrencyAccount, bool>? includeAccount)
    {
        var detected = await DetectPatterns(
            userId,
            asOfDate,
            static entry => entry.ValueChange < 0,
            isIncome: false,
            includeAccount,
            cancellationToken);

        var subscriptions = await subscriptionRepository.GetAll(userId, cancellationToken);
        var added = new List<RecurringSubscription>();
        var matchedIds = new HashSet<Guid>();

        foreach (var pattern in detected)
        {
            var subscription = subscriptions
                .Where(x => !matchedIds.Contains(x.Id))
                .Select(x => (Subscription: x, Similarity: MerchantSimilarity.Calculate(x.MerchantKey, pattern.MerchantKey)))
                .Where(x => x.Similarity >= MerchantSimilarity.Threshold)
                .Where(x => x.Subscription.ReferenceAmount is decimal referenceAmount
                    ? RecurringAmounts.AreSimilar(referenceAmount, pattern.ReferenceAmount)
                    : detected.Count(candidate =>
                        MerchantSimilarity.IsSameMerchant(x.Subscription.MerchantKey, candidate.MerchantKey)) == 1)
                .OrderByDescending(x => x.Similarity)
                .ThenBy(x => x.Subscription.ReferenceAmount is decimal referenceAmount
                    ? Math.Abs(referenceAmount - pattern.ReferenceAmount)
                    : decimal.MaxValue)
                .Select(x => x.Subscription)
                .FirstOrDefault();

            if (subscription is null)
            {
                subscription = new RecurringSubscription
                {
                    Id = CreatePatternId(userId, pattern.MerchantKey, pattern.ReferenceAmount),
                    UserId = userId,
                    MerchantKey = pattern.MerchantKey,
                    Name = pattern.Result.Name,
                    ReferenceAmount = pattern.ReferenceAmount
                };
                subscriptions.Add(subscription);
                added.Add(subscription);
            }
            else
            {
                if (persistSubscriptions)
                {
                    subscription.MerchantKey = pattern.MerchantKey;
                    subscription.Name = pattern.Result.Name;
                    subscription.ReferenceAmount = pattern.ReferenceAmount;
                }
            }

            matchedIds.Add(subscription.Id);
            pattern.Result.PatternId = subscription.Id;
            pattern.Result.IsMuted = subscription.IsMuted;
            pattern.Result.IsCancelled = subscription.IsCancelled;
            pattern.Result.IsFlaggedForReview = subscription.IsFlaggedForReview;
        }

        if (persistSubscriptions)
            await subscriptionRepository.Save(added, cancellationToken);

        return detected
            .Select(x => x.Result)
            .OrderByDescending(x => x.IsFlaggedForReview)
            .ThenBy(x => x.IsMuted || x.IsCancelled)
            .ThenBy(x => x.NextExpectedChargeDate)
            .ToList();
    }

    public async Task<List<RecurringCashFlow>> GetRecurringCashFlows(
        int userId,
        DateTime asOfDate,
        CancellationToken cancellationToken = default)
    {
        // Keep the existing detector as the source of expense/subscription state. This preserves
        // muted/cancelled semantics and stable subscription ids while the second, positive-value
        // pass adds recurring income without changing the subscriptions page contract.
        var expenses = await GetRecurringTransactionsCore(
            userId,
            asOfDate,
            cancellationToken,
            persistSubscriptions: false,
            static account => account.AccountType == AccountLabel.Cash);
        var incomes = await DetectPatterns(
            userId,
            asOfDate,
            static entry => entry.ValueChange > 0,
            isIncome: true,
            static account => account.AccountType == AccountLabel.Cash,
            cancellationToken);

        return expenses
            .Select(expense => new RecurringCashFlow
            {
                Name = expense.Name,
                MonthlyAmount = -expense.MonthlyCost,
                OccurrenceAmount = -expense.LastAmount,
                Cadence = expense.Cadence,
                NextExpectedDate = expense.NextExpectedChargeDate,
                PatternId = expense.PatternId,
                IsMuted = expense.IsMuted,
                IsCancelled = expense.IsCancelled
            })
            .Concat(incomes.Select(income => new RecurringCashFlow
            {
                Name = income.Result.Name,
                MonthlyAmount = income.Result.MonthlyCost,
                OccurrenceAmount = income.Result.LastAmount,
                Cadence = income.Result.Cadence,
                NextExpectedDate = income.Result.NextExpectedChargeDate,
                PatternId = CreatePatternId(userId, income.MerchantKey, income.ReferenceAmount)
            }))
            .OrderBy(flow => flow.NextExpectedDate)
            .ThenBy(flow => flow.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public Task<bool> UpdateSubscription(
        int userId,
        Guid patternId,
        UpdateRecurringSubscription command,
        CancellationToken cancellationToken = default) =>
        subscriptionRepository.Update(userId, patternId, command, cancellationToken);

    // Loads the candidate entries; the recurrence analysis itself lives in RecurringPatternDetector.
    private async Task<List<RecurringPattern>> DetectPatterns(
        int userId,
        DateTime today,
        Func<CurrencyAccountEntry, bool> includeEntry,
        bool isIncome,
        Func<CurrencyAccount, bool>? includeAccount,
        CancellationToken cancellationToken)
    {
        var start = today.AddMonths(-25);
        var end = today.AddDays(1);
        var rawEntries = new List<RecurringEntry>();

        await foreach (var account in financialAccountRepository.GetAccounts<CurrencyAccount>(userId, start, end))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (includeAccount is not null && !includeAccount(account))
                continue;

            foreach (var entry in account.Entries)
            {
                if (!includeEntry(entry) || entry.PostingDate < start || entry.PostingDate >= end)
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

        return RecurringPatternDetector.Detect(rawEntries, today, isIncome);
    }

    private static Guid CreatePatternId(int userId, string merchantKey, decimal referenceAmount)
    {
        var amountKey = referenceAmount.ToString("F2", CultureInfo.InvariantCulture);
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes($"{userId}:{merchantKey}:{amountKey}"));
        return new Guid(hash.AsSpan(0, 16));
    }
}