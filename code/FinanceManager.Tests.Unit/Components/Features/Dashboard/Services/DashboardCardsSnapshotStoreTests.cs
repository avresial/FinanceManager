using FinanceManager.Components.Features.Dashboard.Models;
using FinanceManager.Components.Features.Dashboard.Services;
using FinanceManager.Components.Shared.Models;
using FinanceManager.Components.Shared.Services;
using FinanceManager.Domain.Dashboard.Dtos;
using FinanceManager.Domain.FinancialAccounts.Shared.Entities;
using FinanceManager.Domain.Insights.Entities;
using FinanceManager.Domain.Labels.Entities;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace FinanceManager.Tests.Unit.Components.Features.Dashboard.Services;

[Trait("Category", "Unit")]
public class DashboardCardsSnapshotStoreTests
{
    private readonly Mock<ISnapshotService> _snapshots = new();

    private DashboardCardsSnapshotStore CreateStore() =>
        new(new SnapshotRefreshCoordinator(_snapshots.Object, NullLogger<SnapshotRefreshCoordinator>.Instance));

    [Fact]
    public async Task Insights_PaintsBeforeFetch_AlwaysFetches_AndSkipsUnchangedWrite()
    {
        const string key = "financial-insights:1:all:3";
        _snapshots.Setup(x => x.GetAsync<FinancialInsightsSnapshot>(key))
            .ReturnsAsync(new FinancialInsightsSnapshot { UserId = 1, Count = 3, Insights = [Insight(1)] });
        var painted = false;
        var fetchedAfterPaint = false;

        var result = await CreateStore().RefreshInsightsAsync(
            1,
            3,
            null,
            new RefreshVersionGate(),
            null,
            () =>
            {
                fetchedAfterPaint = painted;
                return Task.FromResult<List<FinancialInsight>?>([Insight(1)]);
            },
            insights =>
            {
                painted = insights.Count == 1;
                return Task.CompletedTask;
            });

        Assert.True(fetchedAfterPaint);
        Assert.Equal(SnapshotRefreshOutcome.Unchanged, result.Outcome);
        _snapshots.Verify(x => x.SetAsync(It.IsAny<string>(), It.IsAny<FinancialInsightsSnapshot>()), Times.Never);
    }

    [Fact]
    public async Task Insights_Changed_WritesCountAndAccountScopedSnapshot()
    {
        const string key = "financial-insights:1:7:5";

        var result = await CreateStore().RefreshInsightsAsync(
            1,
            5,
            7,
            new RefreshVersionGate(),
            null,
            () => Task.FromResult<List<FinancialInsight>?>([Insight(2)]));

        Assert.Equal(SnapshotRefreshOutcome.Refreshed, result.Outcome);
        _snapshots.Verify(x => x.SetAsync(key, It.Is<FinancialInsightsSnapshot>(
            s => s.UserId == 1 && s.Count == 5 && s.AccountId == 7 && s.Insights.Single().Id == 2)), Times.Once);
    }

    [Fact]
    public async Task RecurringTransactions_RejectsAnotherUsersSnapshot()
    {
        const string key = "recurring-transactions:1";
        _snapshots.Setup(x => x.GetAsync<RecurringTransactionsSnapshot>(key))
            .ReturnsAsync(new RecurringTransactionsSnapshot
            {
                UserId = 2,
                Data = [new("Other", 10m)],
                TotalMonthlySpend = 10m
            });
        var painted = false;

        var result = await CreateStore().RefreshRecurringTransactionsAsync(
            1,
            new RefreshVersionGate(),
            null,
            () => Task.FromResult<RecurringTransactionsCardModel?>(new([new("Mine", 20m)], 20m)),
            _ =>
            {
                painted = true;
                return Task.CompletedTask;
            });

        Assert.False(painted);
        Assert.False(result.SnapshotPainted);
        _snapshots.Verify(x => x.SetAsync(key, It.Is<RecurringTransactionsSnapshot>(
            s => s.UserId == 1 && s.TotalMonthlySpend == 20m)), Times.Once);
    }

    [Fact]
    public async Task RecurringTransactions_FailedRefresh_KeepsPaintedModel()
    {
        const string key = "recurring-transactions:1";
        _snapshots.Setup(x => x.GetAsync<RecurringTransactionsSnapshot>(key))
            .ReturnsAsync(new RecurringTransactionsSnapshot
            {
                UserId = 1,
                Data = [new("Stored", 10m)],
                TotalMonthlySpend = 10m
            });

        var result = await CreateStore().RefreshRecurringTransactionsAsync(
            1,
            new RefreshVersionGate(),
            null,
            () => throw new HttpRequestException());

        Assert.Equal(SnapshotRefreshOutcome.Failed, result.Outcome);
        Assert.True(result.SnapshotPainted);
        Assert.False(result.IsBlockingFailure);
        Assert.Equal("Stored", result.Model!.Data.Single().Name);
    }

    [Fact]
    public async Task TransactionLog_NoSnapshotFailure_IsBlocking()
    {
        var result = await CreateStore().RefreshTransactionLogAsync(
            1,
            10,
            new RefreshVersionGate(),
            null,
            () => throw new HttpRequestException());

        Assert.Equal(SnapshotRefreshOutcome.Failed, result.Outcome);
        Assert.True(result.IsBlockingFailure);
    }

    [Fact]
    public async Task TransactionLog_Unchanged_SkipsWrite()
    {
        const string key = "transaction-log:1:10";
        _snapshots.Setup(x => x.GetAsync<TransactionLogSnapshot>(key))
            .ReturnsAsync(new TransactionLogSnapshot { UserId = 1, Count = 10, Data = [Transaction(1)] });

        var result = await CreateStore().RefreshTransactionLogAsync(
            1,
            10,
            new RefreshVersionGate(),
            null,
            () => Task.FromResult<List<TransactionLogEntryDto>?>([Transaction(1)]));

        Assert.Equal(SnapshotRefreshOutcome.Unchanged, result.Outcome);
        _snapshots.Verify(x => x.SetAsync(It.IsAny<string>(), It.IsAny<TransactionLogSnapshot>()), Times.Never);
    }

    [Fact]
    public async Task TransactionLog_SupersededRequest_DoesNotWrite()
    {
        var gate = new RefreshVersionGate();
        var claimed = gate.Claim();

        var result = await CreateStore().RefreshTransactionLogAsync(
            1,
            10,
            gate,
            claimed,
            () =>
            {
                gate.Claim();
                return Task.FromResult<List<TransactionLogEntryDto>?>([Transaction(2)]);
            });

        Assert.Equal(SnapshotRefreshOutcome.Superseded, result.Outcome);
        _snapshots.Verify(x => x.SetAsync(It.IsAny<string>(), It.IsAny<TransactionLogSnapshot>()), Times.Never);
    }

    private static FinancialInsight Insight(int id) => new()
    {
        Id = id,
        UserId = 1,
        Title = $"Insight {id}",
        Message = "Message",
        Tags = "tag",
        CreatedAt = new DateTime(2026, 1, 1)
    };

    private static TransactionLogEntryDto Transaction(long id) =>
        new(1, "Cash", AccountType.Currency, id, new DateTime(2026, 1, 1), -10m, "Entry");
}