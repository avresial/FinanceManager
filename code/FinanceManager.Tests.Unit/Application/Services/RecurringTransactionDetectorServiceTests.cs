using FinanceManager.Application.Labels.RecurringTransactions;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.FinancialAccounts.Shared.Entities;
using FinanceManager.Domain.FinancialAccounts.Shared.Repositories;
using FinanceManager.Domain.Labels.Entities;
using FinanceManager.Domain.Labels.Repositories;
using FinanceManager.Tests.Unit.Shared.Time;
using Moq;

namespace FinanceManager.Tests.Unit.Application.Services;

[Trait("Category", "Unit")]
public class RecurringTransactionDetectorServiceTests
{
    private readonly Mock<IFinancialAccountRepository> _accounts = new();
    private readonly Mock<IRecurringSubscriptionRepository> _subscriptions = new();
    private readonly FakeDateTimeProvider _clock = new(new DateTime(2026, 7, 29));

    [Fact]
    public async Task GetRecurringTransactions_CalculatesMonthlyCostNextChargeAndDelta()
    {
        var existingId = Guid.NewGuid();
        var account = Account(
            Entry(1, new DateTime(2026, 4, 15), -40m, "Netflix"),
            Entry(2, new DateTime(2026, 5, 15), -43m, "Netflix"),
            Entry(3, new DateTime(2026, 6, 15), -49m, "Netflix"));
        Setup(account,
        [
            new RecurringSubscription
            {
                Id = existingId,
                UserId = 7,
                MerchantKey = "netflix",
                Name = "NETFLIX",
                IsFlaggedForReview = true
            }
        ]);
        var service = CreateService();

        var result = Assert.Single(await service.GetRecurringTransactions(
            7,
            TestContext.Current.CancellationToken));

        Assert.Equal(existingId, result.PatternId);
        Assert.Equal(RecurringCadence.Monthly, result.Cadence);
        Assert.Equal(new DateTime(2026, 8, 15), result.NextExpectedChargeDate);
        Assert.Equal(49m, result.LastAmount);
        Assert.Equal(6m, result.PriceDelta);
        Assert.Equal(44m, result.MonthlyCost);
        Assert.Equal(528m, result.AnnualCost);
        Assert.True(result.IsFlaggedForReview);
    }

    [Fact]
    public async Task GetRecurringTransactions_DetectsAnnualCadenceAndPersistsStablePattern()
    {
        var account = Account(
            Entry(1, new DateTime(2025, 6, 1), -120m, "Cloud storage"),
            Entry(2, new DateTime(2026, 6, 1), -144m, "Cloud storage"));
        Setup(account, []);
        List<RecurringSubscription>? saved = null;
        _subscriptions
            .Setup(x => x.Save(It.IsAny<IEnumerable<RecurringSubscription>>(), It.IsAny<CancellationToken>()))
            .Callback<IEnumerable<RecurringSubscription>, CancellationToken>((items, _) => saved = items.ToList())
            .Returns(Task.CompletedTask);
        var service = CreateService();

        var result = Assert.Single(await service.GetRecurringTransactions(
            7,
            TestContext.Current.CancellationToken));

        Assert.Equal(RecurringCadence.Annual, result.Cadence);
        Assert.Equal(11m, result.MonthlyCost);
        Assert.Equal(132m, result.AnnualCost);
        Assert.Equal(new DateTime(2027, 6, 1), result.NextExpectedChargeDate);
        Assert.NotEqual(Guid.Empty, result.PatternId);
        Assert.Equal(result.PatternId, Assert.Single(saved!).Id);
    }

    [Fact]
    public async Task GetRecurringTransactions_DetectsWeeklyCadence()
    {
        var account = Account(
            Entry(1, new DateTime(2026, 7, 1), -12m, "Weekly pass"),
            Entry(2, new DateTime(2026, 7, 8), -12m, "Weekly pass"),
            Entry(3, new DateTime(2026, 7, 15), -12m, "Weekly pass"),
            Entry(4, new DateTime(2026, 7, 22), -12m, "Weekly pass"));
        Setup(account, []);

        var result = Assert.Single(await CreateService().GetRecurringTransactions(
            7,
            TestContext.Current.CancellationToken));

        Assert.Equal(RecurringCadence.Weekly, result.Cadence);
        Assert.Equal(52m, result.MonthlyCost);
        Assert.Equal(new DateTime(2026, 8, 5), result.NextExpectedChargeDate);
    }

    [Fact]
    public async Task GetRecurringTransactions_DetectsQuarterlyCadence()
    {
        var account = Account(
            Entry(1, new DateTime(2025, 12, 1), -90m, "Quarterly pass"),
            Entry(2, new DateTime(2026, 3, 1), -90m, "Quarterly pass"),
            Entry(3, new DateTime(2026, 6, 1), -90m, "Quarterly pass"));
        Setup(account, []);

        var result = Assert.Single(await CreateService().GetRecurringTransactions(
            7,
            TestContext.Current.CancellationToken));

        Assert.Equal(RecurringCadence.Quarterly, result.Cadence);
        Assert.Equal(30m, result.MonthlyCost);
        Assert.Equal(new DateTime(2026, 9, 1), result.NextExpectedChargeDate);
    }

    private RecurringTransactionDetectorService CreateService() =>
        new(_accounts.Object, _subscriptions.Object, _clock);

    private void Setup(CurrencyAccount account, List<RecurringSubscription> subscriptions)
    {
        _accounts
            .Setup(x => x.GetAccounts<CurrencyAccount>(
                7,
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                It.IsAny<bool>()))
            .Returns(new[] { account }.ToAsyncEnumerable());
        _subscriptions
            .Setup(x => x.GetAll(7, It.IsAny<CancellationToken>()))
            .ReturnsAsync(subscriptions);
        _subscriptions
            .Setup(x => x.Save(It.IsAny<IEnumerable<RecurringSubscription>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
    }

    private static CurrencyAccount Account(params CurrencyAccountEntry[] entries) =>
        new(7, 1, "Cash", entries, AccountLabel.Cash);

    private static CurrencyAccountEntry Entry(int id, DateTime date, decimal amount, string merchant) =>
        new(1, id, date, amount, amount)
        {
            ContractorDetails = merchant,
            Description = merchant
        };
}