using FinanceManager.Domain.Alerts.Entities;
using FinanceManager.Domain.Alerts.Enums;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.FinancialAccounts.Shared.Dtos;
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
        context.Accounts.Add(new FinancialAccountBaseDto
        {
            AccountId = 1,
            UserId = 1,
            Name = "Cash",
            AccountType = AccountType.Currency,
            AccountLabel = AccountLabel.Cash
        });
        context.CurrencyEntries.AddRange(
            new CurrencyAccountEntry(1, 1, new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc), 400m, -400m)
            {
                Labels = [dining]
            },
            new CurrencyAccountEntry(1, 2, new DateTime(2026, 9, 2, 0, 0, 0, DateTimeKind.Utc), 3300m, -2900m)
            {
                Labels = [dining]
            });
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var categoryAlert = new FinancialAlert(
            1,
            "Dining total",
            AlertType.CategorySpending,
            AlertComparisonOperator.GreaterThan,
            1000m,
            evaluationPeriod: AlertEvaluationPeriod.AllTime,
            labelName: "dining");
        var largeTransactionAlert = new FinancialAlert(
            1,
            "Large purchase",
            AlertType.LargeTransaction,
            AlertComparisonOperator.GreaterThan,
            2000m,
            evaluationPeriod: AlertEvaluationPeriod.AllTime)
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

        var largeData = result[largeTransactionAlert.Id];
        Assert.Equal(1, largeData.TransactionCount);
        Assert.Equal(2, largeData.LargestTransaction!.EntryId);
        Assert.Equal(2900m, Math.Abs(largeData.LargestTransaction.ValueChange));
        Assert.Equal([2], largeData.MatchingTransactions!.Select(entry => entry.EntryId));
    }

    [Fact]
    public async Task GetAllTimeEvaluationData_DetailedRequestReturnsEveryMatchingTransaction()
    {
        await using var context = CreateContext();
        var label = new FinancialLabel { Id = 5, Name = "Dining" };
        context.FinancialLabels.Add(label);
        context.Accounts.Add(new FinancialAccountBaseDto
        {
            AccountId = 1,
            UserId = 1,
            Name = "Cash",
            AccountType = AccountType.Currency,
            AccountLabel = AccountLabel.Cash
        });

        for (var entryId = 1; entryId <= 6; entryId++)
        {
            context.CurrencyEntries.Add(new CurrencyAccountEntry(
                1,
                entryId,
                new DateTime(2026, 9, entryId, 0, 0, 0, DateTimeKind.Utc),
                1000m - entryId * 100m,
                -entryId * 100m)
            {
                Labels = [label]
            });
        }

        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        var alert = new FinancialAlert(
            1,
            "Dining total",
            AlertType.CategorySpending,
            AlertComparisonOperator.GreaterThan,
            100m,
            evaluationPeriod: AlertEvaluationPeriod.AllTime,
            labelName: "dining");
        var repository = new FinancialAlertRepository(context);

        var summary = await repository.GetAllTimeEvaluationData(
            1,
            [alert],
            new DateTime(2026, 9, 11, 0, 0, 0, DateTimeKind.Utc),
            TestContext.Current.CancellationToken);
        var detailed = await repository.GetAllTimeEvaluationData(
            1,
            [alert],
            new DateTime(2026, 9, 11, 0, 0, 0, DateTimeKind.Utc),
            TestContext.Current.CancellationToken,
            includeAllMatchingTransactions: true);

        Assert.Equal(6, summary[alert.Id].TransactionCount);
        Assert.Equal(5, summary[alert.Id].MatchingTransactions!.Count);
        Assert.Equal(6, detailed[alert.Id].MatchingTransactions!.Count);
        Assert.Equal(Enumerable.Range(1, 6).Reverse(), detailed[alert.Id].MatchingTransactions!.Select(entry => entry.EntryId));
    }

    private static FinancialAlert CreateAlert(int userId, string title) =>
        new(userId, title, AlertType.AccountBalance, AlertComparisonOperator.LessThan, 1000m);

    private static AppDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);
}