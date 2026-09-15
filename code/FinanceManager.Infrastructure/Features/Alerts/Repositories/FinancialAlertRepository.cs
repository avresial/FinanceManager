using FinanceManager.Domain.Alerts.Entities;
using FinanceManager.Domain.Alerts.Enums;
using FinanceManager.Domain.Alerts.Models;
using FinanceManager.Domain.Alerts.Repositories;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FinanceManager.Infrastructure.Features.Alerts.Repositories;

internal sealed class FinancialAlertRepository(AppDbContext context) : IFinancialAlertRepository
{
    private const int _matchingTransactionLimit = 5;

    public async Task<IReadOnlyList<FinancialAlert>> GetAlertsByUserId(
        int userId,
        CancellationToken cancellationToken = default) =>
        await context.FinancialAlerts
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .OrderBy(x => x.CreatedAt)
            .ThenBy(x => x.Id)
            .ToListAsync(cancellationToken);

    public Task<FinancialAlert?> GetById(
        int userId,
        Guid alertId,
        CancellationToken cancellationToken = default) =>
        context.FinancialAlerts
            .SingleOrDefaultAsync(x => x.UserId == userId && x.Id == alertId, cancellationToken);

    public async Task<FinancialAlert> Add(
        FinancialAlert alert,
        CancellationToken cancellationToken = default)
    {
        context.FinancialAlerts.Add(alert);
        await context.SaveChangesAsync(cancellationToken);
        return alert;
    }

    public async Task Update(
        FinancialAlert alert,
        CancellationToken cancellationToken = default)
    {
        context.FinancialAlerts.Update(alert);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> Delete(
        int userId,
        Guid alertId,
        CancellationToken cancellationToken = default)
    {
        var alert = await context.FinancialAlerts
            .SingleOrDefaultAsync(x => x.UserId == userId && x.Id == alertId, cancellationToken);
        if (alert is null)
            return false;

        context.FinancialAlerts.Remove(alert);
        await context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<IReadOnlyDictionary<Guid, FinancialAlertEvaluationData>> GetAllTimeEvaluationData(
        int userId,
        IReadOnlyCollection<FinancialAlert> alerts,
        DateTime endDate,
        CancellationToken cancellationToken = default)
    {
        if (alerts.Count == 0)
            return new Dictionary<Guid, FinancialAlertEvaluationData>();

        var result = alerts.ToDictionary(
            alert => alert.Id,
            _ => new FinancialAlertEvaluationData(0m, 0, null));
        IQueryable<AlertEvaluationAggregateRow>? aggregateQuery = null;
        IQueryable<AlertEvaluationAggregateRow>? matchingQuery = null;

        foreach (var alert in alerts)
        {
            var entries = GetUserEntries(userId, endDate);
            IQueryable<AlertEvaluationAggregateRow>? alertQuery = alert.AlertType switch
            {
                AlertType.CategorySpending => BuildCategoryAggregate(entries, alert),
                AlertType.MerchantSpending => BuildMerchantAggregate(entries, alert),
                AlertType.LargeTransaction => BuildLargeTransactionAggregate(entries, alert),
                _ => null
            };

            if (alertQuery is not null)
            {
                aggregateQuery = aggregateQuery is null ? alertQuery : aggregateQuery.Concat(alertQuery);
                var alertMatchingQuery = BuildMatchingTransactionRows(entries, alert);
                matchingQuery = matchingQuery is null ? alertMatchingQuery : matchingQuery.Concat(alertMatchingQuery);
            }
        }

        if (aggregateQuery is null)
            return result;

        var evaluationQuery = matchingQuery is null
            ? aggregateQuery
            : aggregateQuery.Concat(matchingQuery);
        var matchingTransactions = new Dictionary<Guid, List<CurrencyAccountEntry>>();
        foreach (var row in await evaluationQuery.ToListAsync(cancellationToken))
        {
            if (row.MatchingEntryId is int)
            {
                matchingTransactions.TryAdd(row.AlertId, []);
                matchingTransactions[row.AlertId].Add(row.ToMatchingTransaction());
                continue;
            }

            result[row.AlertId] = row.ToEvaluationData();
        }

        foreach (var (alertId, entries) in matchingTransactions)
        {
            result[alertId] = result[alertId] with { MatchingTransactions = entries };
        }

        return result;
    }

    private IQueryable<CurrencyAccountEntry> GetUserEntries(int userId, DateTime endDate) =>
        context.CurrencyEntries
            .AsNoTracking()
            .Where(entry => entry.PostingDate <= endDate)
            .Where(entry => context.Accounts
                .AsNoTracking()
                .Where(account => account.UserId == userId)
                .Select(account => account.AccountId)
                .Contains(entry.AccountId));

    private static IQueryable<CurrencyAccountEntry> BuildMatchingEntries(
        IQueryable<CurrencyAccountEntry> entries,
        FinancialAlert alert) => alert.AlertType switch
        {
            AlertType.CategorySpending => BuildCategoryEntries(entries, alert),
            AlertType.MerchantSpending => BuildMerchantEntries(entries, alert),
            AlertType.LargeTransaction => BuildLargeTransactionEntries(entries, alert),
            _ => entries.Where(_ => false)
        };

    private static IQueryable<AlertEvaluationAggregateRow> BuildMatchingTransactionRows(
        IQueryable<CurrencyAccountEntry> entries,
        FinancialAlert alert) =>
        BuildMatchingEntries(entries, alert)
            .OrderByDescending(entry => -entry.ValueChange)
            .ThenByDescending(entry => entry.PostingDate)
            .ThenByDescending(entry => entry.EntryId)
            .Take(_matchingTransactionLimit)
            .Select(entry => new AlertEvaluationAggregateRow
            {
                AlertId = alert.Id,
                TotalSpend = 0m,
                TransactionCount = 0,
                LargestAccountId = null,
                LargestEntryId = null,
                LargestPostingDate = null,
                LargestValue = null,
                LargestValueChange = null,
                LargestDescription = null,
                LargestContractorDetails = null,
                MatchingAccountId = entry.AccountId,
                MatchingEntryId = entry.EntryId,
                MatchingPostingDate = entry.PostingDate,
                MatchingValue = entry.Value,
                MatchingValueChange = entry.ValueChange,
                MatchingDescription = entry.Description,
                MatchingContractorDetails = entry.ContractorDetails
            });

    private static IQueryable<CurrencyAccountEntry> BuildCategoryEntries(
        IQueryable<CurrencyAccountEntry> entries,
        FinancialAlert alert)
    {
        entries = entries.Where(entry => entry.ValueChange < 0);
        if (alert.AccountId is int accountId)
            entries = entries.Where(entry => entry.AccountId == accountId);

        if (alert.LabelId is int labelId)
        {
            entries = entries.Where(entry => entry.Labels.Any(label => label.Id == labelId));
        }
        else if (!string.IsNullOrWhiteSpace(alert.LabelName))
        {
            var labelName = alert.LabelName.Trim().ToLowerInvariant();
            entries = entries.Where(entry => entry.Labels.Any(label => label.Name.ToLower() == labelName));
        }
        else
        {
            entries = entries.Where(entry => entry.Labels.Any());
        }

        return entries;
    }

    private static IQueryable<CurrencyAccountEntry> BuildMerchantEntries(
        IQueryable<CurrencyAccountEntry> entries,
        FinancialAlert alert)
    {
        entries = entries.Where(entry => entry.ValueChange < 0);
        if (alert.AccountId is int accountId)
            entries = entries.Where(entry => entry.AccountId == accountId);

        var merchantName = alert.MerchantName?.Trim().ToLowerInvariant() ?? string.Empty;
        if (merchantName.Length > 0)
        {
            entries = entries.Where(entry =>
                entry.Description.ToLower().Contains(merchantName)
                || (entry.ContractorDetails != null && entry.ContractorDetails.ToLower().Contains(merchantName)));
        }

        return entries;
    }

    private static IQueryable<CurrencyAccountEntry> BuildLargeTransactionEntries(
        IQueryable<CurrencyAccountEntry> entries,
        FinancialAlert alert)
    {
        if (alert.AccountId is int accountId)
            entries = entries.Where(entry => entry.AccountId == accountId);
        if (alert.CreatedAt != default)
            entries = entries.Where(entry => entry.PostingDate >= alert.CreatedAt.Date);

        entries = entries.Where(entry => entry.ValueChange < 0);
        var threshold = alert.Threshold;
        return alert.ComparisonOperator switch
        {
            AlertComparisonOperator.GreaterThan => entries.Where(entry => -entry.ValueChange > threshold),
            AlertComparisonOperator.GreaterThanOrEqual => entries.Where(entry => -entry.ValueChange >= threshold),
            AlertComparisonOperator.LessThan => entries.Where(entry => -entry.ValueChange < threshold),
            AlertComparisonOperator.LessThanOrEqual => entries.Where(entry => -entry.ValueChange <= threshold),
            AlertComparisonOperator.Equal => entries.Where(entry => -entry.ValueChange == threshold),
            AlertComparisonOperator.NotEqual => entries.Where(entry => -entry.ValueChange != threshold),
            _ => entries.Where(_ => false)
        };
    }

    private static IQueryable<AlertEvaluationAggregateRow> BuildCategoryAggregate(
        IQueryable<CurrencyAccountEntry> entries,
        FinancialAlert alert)
    {
        entries = BuildCategoryEntries(entries, alert);

        return entries
            .GroupBy(_ => 1)
            .Select(group => new AlertEvaluationAggregateRow
            {
                AlertId = alert.Id,
                TotalSpend = group.Sum(entry => -entry.ValueChange),
                TransactionCount = group.Count(),
                LargestAccountId = null,
                LargestEntryId = null,
                LargestPostingDate = null,
                LargestValue = null,
                LargestValueChange = null,
                LargestDescription = null,
                LargestContractorDetails = null,
                MatchingAccountId = null,
                MatchingEntryId = null,
                MatchingPostingDate = null,
                MatchingValue = null,
                MatchingValueChange = null,
                MatchingDescription = null,
                MatchingContractorDetails = null
            });
    }

    private static IQueryable<AlertEvaluationAggregateRow> BuildMerchantAggregate(
        IQueryable<CurrencyAccountEntry> entries,
        FinancialAlert alert)
    {
        entries = BuildMerchantEntries(entries, alert);

        return entries
            .GroupBy(_ => 1)
            .Select(group => new AlertEvaluationAggregateRow
            {
                AlertId = alert.Id,
                TotalSpend = group.Sum(entry => -entry.ValueChange),
                TransactionCount = group.Count(),
                LargestAccountId = null,
                LargestEntryId = null,
                LargestPostingDate = null,
                LargestValue = null,
                LargestValueChange = null,
                LargestDescription = null,
                LargestContractorDetails = null,
                MatchingAccountId = null,
                MatchingEntryId = null,
                MatchingPostingDate = null,
                MatchingValue = null,
                MatchingValueChange = null,
                MatchingDescription = null,
                MatchingContractorDetails = null
            });
    }

    private static IQueryable<AlertEvaluationAggregateRow> BuildLargeTransactionAggregate(
        IQueryable<CurrencyAccountEntry> entries,
        FinancialAlert alert)
    {
        entries = BuildLargeTransactionEntries(entries, alert);

        return entries
            .OrderByDescending(entry => -entry.ValueChange)
            .ThenByDescending(entry => entry.PostingDate)
            .ThenByDescending(entry => entry.EntryId)
            .Select(entry => new AlertEvaluationAggregateRow
            {
                AlertId = alert.Id,
                TotalSpend = 0m,
                TransactionCount = entries.Count(),
                LargestAccountId = entry.AccountId,
                LargestEntryId = entry.EntryId,
                LargestPostingDate = entry.PostingDate,
                LargestValue = entry.Value,
                LargestValueChange = entry.ValueChange,
                LargestDescription = entry.Description,
                LargestContractorDetails = entry.ContractorDetails,
                MatchingAccountId = null,
                MatchingEntryId = null,
                MatchingPostingDate = null,
                MatchingValue = null,
                MatchingValueChange = null,
                MatchingDescription = null,
                MatchingContractorDetails = null
            })
            .Take(1);
    }

    private sealed class AlertEvaluationAggregateRow
    {
        public Guid AlertId { get; init; }
        public decimal TotalSpend { get; init; }
        public int TransactionCount { get; init; }
        public int? LargestAccountId { get; init; }
        public int? LargestEntryId { get; init; }
        public DateTime? LargestPostingDate { get; init; }
        public decimal? LargestValue { get; init; }
        public decimal? LargestValueChange { get; init; }
        public string? LargestDescription { get; init; }
        public string? LargestContractorDetails { get; init; }
        public int? MatchingAccountId { get; init; }
        public int? MatchingEntryId { get; init; }
        public DateTime? MatchingPostingDate { get; init; }
        public decimal? MatchingValue { get; init; }
        public decimal? MatchingValueChange { get; init; }
        public string? MatchingDescription { get; init; }
        public string? MatchingContractorDetails { get; init; }

        public FinancialAlertEvaluationData ToEvaluationData()
        {
            CurrencyAccountEntry? largestTransaction = null;
            if (LargestAccountId is int accountId
                && LargestEntryId is int entryId
                && LargestPostingDate is DateTime postingDate
                && LargestValue is decimal value
                && LargestValueChange is decimal valueChange)
            {
                largestTransaction = new CurrencyAccountEntry(accountId, entryId, postingDate, value, valueChange)
                {
                    Description = LargestDescription ?? string.Empty,
                    ContractorDetails = LargestContractorDetails
                };
            }

            return new FinancialAlertEvaluationData(TotalSpend, TransactionCount, largestTransaction);
        }

        public CurrencyAccountEntry ToMatchingTransaction()
        {
            if (MatchingAccountId is not int accountId
                || MatchingEntryId is not int entryId
                || MatchingPostingDate is not DateTime postingDate
                || MatchingValue is not decimal value
                || MatchingValueChange is not decimal valueChange)
            {
                throw new InvalidOperationException("A matching transaction row is incomplete.");
            }

            return new CurrencyAccountEntry(accountId, entryId, postingDate, value, valueChange)
            {
                Description = MatchingDescription ?? string.Empty,
                ContractorDetails = MatchingContractorDetails
            };
        }
    }
}