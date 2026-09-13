using FinanceManager.Domain.Alerts.Entities;
using FinanceManager.Domain.Alerts.Enums;
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

    private static FinancialAlert CreateAlert(int userId, string title) =>
        new(userId, title, AlertType.AccountBalance, AlertComparisonOperator.LessThan, 1000m);

    private static AppDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);
}