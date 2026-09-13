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
                aggregateQuery = aggregateQuery is null ? alertQuery : aggregateQuery.Concat(alertQuery);
        }

        if (aggregateQuery is null)
            return result;

        foreach (var row in await aggregateQuery.ToListAsync(cancellationToken))
            result[row.AlertId] = row.ToEvaluationData();

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

    private static IQueryable<AlertEvaluationAggregateRow> BuildCategoryAggregate(
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
                LargestContractorDetails = null
            });
    }

    private static IQueryable<AlertEvaluationAggregateRow> BuildMerchantAggregate(
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
                LargestContractorDetails = null
            });
    }

    private static IQueryable<AlertEvaluationAggregateRow> BuildLargeTransactionAggregate(
        IQueryable<CurrencyAccountEntry> entries,
        FinancialAlert alert)
    {
        if (alert.AccountId is int accountId)
            entries = entries.Where(entry => entry.AccountId == accountId);
        if (alert.CreatedAt != default)
            entries = entries.Where(entry => entry.PostingDate >= alert.CreatedAt.Date);

        entries = entries.Where(entry => entry.ValueChange < 0);
        var threshold = alert.Threshold;
        entries = alert.ComparisonOperator switch
        {
            AlertComparisonOperator.GreaterThan => entries.Where(entry => -entry.ValueChange > threshold),
            AlertComparisonOperator.GreaterThanOrEqual => entries.Where(entry => -entry.ValueChange >= threshold),
            AlertComparisonOperator.LessThan => entries.Where(entry => -entry.ValueChange < threshold),
            AlertComparisonOperator.LessThanOrEqual => entries.Where(entry => -entry.ValueChange <= threshold),
            AlertComparisonOperator.Equal => entries.Where(entry => -entry.ValueChange == threshold),
            AlertComparisonOperator.NotEqual => entries.Where(entry => -entry.ValueChange != threshold),
            _ => entries.Where(_ => false)
        };

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
                LargestContractorDetails = entry.ContractorDetails
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
    }
}