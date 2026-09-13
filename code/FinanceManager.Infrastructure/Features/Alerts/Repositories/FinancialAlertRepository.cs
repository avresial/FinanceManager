using FinanceManager.Domain.Alerts.Entities;
using FinanceManager.Domain.Alerts.Enums;
using FinanceManager.Domain.Alerts.Models;
using FinanceManager.Domain.Alerts.Repositories;
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

        var userAccountIds = context.Accounts
            .AsNoTracking()
            .Where(account => account.UserId == userId)
            .Select(account => account.AccountId);
        var result = new Dictionary<Guid, FinancialAlertEvaluationData>();

        foreach (var alert in alerts)
        {
            var entries = context.CurrencyEntries
                .AsNoTracking()
                .Where(entry => entry.PostingDate <= endDate)
                .Where(entry => userAccountIds.Contains(entry.AccountId));

            if (alert.AccountId is int accountId)
                entries = entries.Where(entry => entry.AccountId == accountId);

            switch (alert.AlertType)
            {
                case AlertType.CategorySpending:
                    {
                        entries = entries.Where(entry => entry.ValueChange < 0);
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

                        var categoryAggregate = await entries
                            .GroupBy(_ => 1)
                            .Select(group => new
                            {
                                TotalSpend = group.Sum(entry => -entry.ValueChange),
                                TransactionCount = group.Count()
                            })
                            .SingleOrDefaultAsync(cancellationToken);

                        result[alert.Id] = categoryAggregate is null
                            ? new FinancialAlertEvaluationData(0m, 0, null)
                            : new FinancialAlertEvaluationData(
                                categoryAggregate.TotalSpend,
                                categoryAggregate.TransactionCount,
                                null);
                        break;
                    }

                case AlertType.MerchantSpending:
                    {
                        entries = entries.Where(entry => entry.ValueChange < 0);
                        var merchantName = alert.MerchantName?.Trim().ToLowerInvariant() ?? string.Empty;
                        if (merchantName.Length > 0)
                        {
                            entries = entries.Where(entry =>
                                entry.Description.ToLower().Contains(merchantName)
                                || (entry.ContractorDetails != null && entry.ContractorDetails.ToLower().Contains(merchantName)));
                        }

                        var merchantAggregate = await entries
                            .GroupBy(_ => 1)
                            .Select(group => new
                            {
                                TotalSpend = group.Sum(entry => -entry.ValueChange),
                                TransactionCount = group.Count()
                            })
                            .SingleOrDefaultAsync(cancellationToken);

                        result[alert.Id] = merchantAggregate is null
                            ? new FinancialAlertEvaluationData(0m, 0, null)
                            : new FinancialAlertEvaluationData(
                                merchantAggregate.TotalSpend,
                                merchantAggregate.TransactionCount,
                                null);
                        break;
                    }

                case AlertType.LargeTransaction:
                    {
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

                        var transactionCount = await entries.CountAsync(cancellationToken);
                        var largestTransaction = await entries
                            .OrderByDescending(entry => -entry.ValueChange)
                            .ThenByDescending(entry => entry.PostingDate)
                            .ThenByDescending(entry => entry.EntryId)
                            .FirstOrDefaultAsync(cancellationToken);

                        result[alert.Id] = new FinancialAlertEvaluationData(0m, transactionCount, largestTransaction);
                        break;
                    }
            }
        }

        return result;
    }
}