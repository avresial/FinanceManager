using FinanceManager.Domain.Alerts.Entities;
using FinanceManager.Domain.Alerts.Enums;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.FinancialAccounts.Shared.Entities;
using FinanceManager.Infrastructure.Features.Alerts.Repositories;
using FinanceManager.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FinanceManager.Tests.Unit.Infrastructure.Features.Alerts.Repositories;

[Collection("Infrastructure")]
[Trait("Category", "Unit")]
public sealed class FinancialAlertRepositoryTests
{
    [Fact]
    public async Task GetAlertsByUserId_FiltersByOwnerAndOrdersByCreation()
    {
        await using var context = CreateContext();
        var older = CreateAlert(1, "Older");
        older.CreatedAt = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc);
        var newer = CreateAlert(1, "Newer");
        newer.CreatedAt = older.CreatedAt.AddMinutes(1);
        context.FinancialAlerts.AddRange(
            newer,
            older,
            CreateAlert(2, "Another user"));
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await new FinancialAlertRepository(context)
            .GetAlertsByUserId(1, TestContext.Current.CancellationToken);

        Assert.Equal([older.Id, newer.Id], result.Select(x => x.Id));
    }

    [Fact]
    public async Task GetById_AndDelete_EnforceOwner()
    {
        await using var context = CreateContext();
        var alert = CreateAlert(1, "Owned");
        context.FinancialAlerts.Add(alert);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        var repository = new FinancialAlertRepository(context);

        Assert.Null(await repository.GetById(2, alert.Id, TestContext.Current.CancellationToken));
        Assert.False(await repository.Delete(2, alert.Id, TestContext.Current.CancellationToken));
        Assert.NotNull(await repository.GetById(1, alert.Id, TestContext.Current.CancellationToken));

        Assert.True(await repository.Delete(1, alert.Id, TestContext.Current.CancellationToken));
        Assert.Null(await repository.GetById(1, alert.Id, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task AddAndUpdate_PersistsAlertState()
    {
        await using var context = CreateContext();
        var repository = new FinancialAlertRepository(context);
        var alert = await repository.Add(CreateAlert(1, "Before"), TestContext.Current.CancellationToken);

        alert.Title = "After";
        alert.LastStatus = AlertTriggerStatus.Triggered;
        alert.LastTriggeredValue = 2500m;
        await repository.Update(alert, TestContext.Current.CancellationToken);

        var persisted = await repository.GetById(1, alert.Id, TestContext.Current.CancellationToken);
        Assert.Equal("After", persisted!.Title);
        Assert.Equal(AlertTriggerStatus.Triggered, persisted.LastStatus);
        Assert.Equal(2500m, persisted.LastTriggeredValue);
    }

    [Fact]
    public async Task GetAllTimeEvaluationData_AggregatesSpendingAndSelectsLargestTransaction()
    {
        await using var context = CreateContext();
        var dining = new FinancialLabel { Id = 5, Name = "Dining" };
        context.FinancialLabels.Add(dining);
        context.CurrencyEntries.AddRange(
            Entry(1, 1, 0, new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc), -400m, dining),
            Entry(2, 1, 0, new DateTime(2026, 9, 2, 0, 0, 0, DateTimeKind.Utc), -2900m, dining),
            // Another currency of the same owner, another owner and an income are not part of the totals.
            Entry(3, 1, 1, new DateTime(2026, 9, 3, 0, 0, 0, DateTimeKind.Utc), -5000m, dining),
            Entry(4, 2, 0, new DateTime(2026, 9, 3, 0, 0, 0, DateTimeKind.Utc), -7000m, dining),
            Entry(5, 1, 0, new DateTime(2026, 9, 4, 0, 0, 0, DateTimeKind.Utc), 800m, dining));
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var categoryAlert = new FinancialAlert(
            1,
            "Dining total",
            AlertType.CategorySpending,
            AlertComparisonOperator.GreaterThan,
            1000m,
            evaluationPeriod: AlertEvaluationPeriod.AllTime,
            currencyId: 0,
            labelName: "dining");
        var largeTransactionAlert = new FinancialAlert(
            1,
            "Large purchase",
            AlertType.LargeTransaction,
            AlertComparisonOperator.GreaterThan,
            2000m,
            evaluationPeriod: AlertEvaluationPeriod.AllTime,
            currencyId: 0)
        {
            CreatedAt = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc)
        };

        var result = await new FinancialAlertRepository(context).GetAllTimeEvaluationData(
            1,
            [categoryAlert, largeTransactionAlert],
            new DateTime(2026, 9, 11, 0, 0, 0, DateTimeKind.Utc),
            TestContext.Current.CancellationToken);

        var categoryData = result[categoryAlert.Id];
        Assert.Equal(3300m, categoryData.TotalSpend);
        Assert.Equal(2, categoryData.TransactionCount);
        Assert.Equal([2, 1], categoryData.MatchingTransactions!.Select(entry => entry.EntryId));
        Assert.All(categoryData.MatchingTransactions!, entry => Assert.Equal(0, entry.CurrencyId));

        var largeData = result[largeTransactionAlert.Id];
        Assert.Equal(1, largeData.TransactionCount);
        Assert.Equal(2, largeData.LargestTransaction!.EntryId);
        Assert.Equal(0, largeData.LargestTransaction.CurrencyId);
        Assert.Equal(2900m, Math.Abs(largeData.LargestTransaction.ValueChange));
        Assert.Equal([2], largeData.MatchingTransactions!.Select(entry => entry.EntryId));
    }

    [Fact]
    public async Task GetAllTimeEvaluationData_ReadsEntriesWithoutAnAccountOfANegativeGuestOwner()
    {
        await using var context = CreateContext();
        context.CurrencyEntries.AddRange(
            Entry(1, -3, 1, new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc), -300m),
            Entry(2, -3, 0, new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc), -999m));
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        var alert = new FinancialAlert(-3, "Merchant", AlertType.MerchantSpending, AlertComparisonOperator.GreaterThan, 10m,
            evaluationPeriod: AlertEvaluationPeriod.AllTime, currencyId: 1);

        var result = await new FinancialAlertRepository(context).GetAllTimeEvaluationData(
            -3, [alert], new DateTime(2026, 9, 11, 0, 0, 0, DateTimeKind.Utc), TestContext.Current.CancellationToken);

        Assert.Equal(300m, result[alert.Id].TotalSpend);
        Assert.Equal(1, result[alert.Id].TransactionCount);
    }

    [Fact]
    public async Task GetAllTimeEvaluationData_AlertWithoutCurrencyMatchesNothing()
    {
        await using var context = CreateContext();
        context.CurrencyEntries.Add(Entry(1, 1, 0, new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc), -300m));
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        var alert = new FinancialAlert(1, "Migrated", AlertType.MerchantSpending, AlertComparisonOperator.GreaterThan, 10m,
            evaluationPeriod: AlertEvaluationPeriod.AllTime);

        var result = await new FinancialAlertRepository(context).GetAllTimeEvaluationData(
            1, [alert], new DateTime(2026, 9, 11, 0, 0, 0, DateTimeKind.Utc), TestContext.Current.CancellationToken);

        Assert.Equal(0m, result[alert.Id].TotalSpend);
        Assert.Equal(0, result[alert.Id].TransactionCount);
    }

    // Owner-keyed entry with no account link, as written by the account-free transaction API.
    private static CurrencyAccountEntry Entry(long id, int userId, int currencyId, DateTime postingDate, decimal change, FinancialLabel? label = null) =>
        new(null, id, postingDate, 0m, change)
        {
            UserId = userId,
            CurrencyId = currencyId,
            Labels = label is null ? [] : [label]
        };

    private static FinancialAlert CreateAlert(int userId, string title) =>
        new(userId, title, AlertType.AccountBalance, AlertComparisonOperator.LessThan, 1000m);

    private static AppDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);
}