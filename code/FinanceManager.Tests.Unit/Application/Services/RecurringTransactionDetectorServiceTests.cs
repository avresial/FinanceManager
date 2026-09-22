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
            Entry(2, new DateTime(2026, 5, 15), -41m, "Netflix"),
            Entry(3, new DateTime(2026, 6, 15), -42m, "Netflix"));
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
        Assert.Equal(42m, result.LastAmount);
        Assert.Equal(1m, result.PriceDelta);
        Assert.Equal(41m, result.MonthlyCost);
        Assert.Equal(492m, result.AnnualCost);
        Assert.True(result.IsFlaggedForReview);
    }

    [Fact]
    public async Task GetRecurringTransactions_DetectsAnnualCadenceAndPersistsStablePattern()
    {
        var account = Account(
            Entry(1, new DateTime(2025, 6, 1), -120m, "Cloud storage"),
            Entry(2, new DateTime(2026, 6, 1), -124m, "Cloud storage"));
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
        Assert.Equal(10.17m, result.MonthlyCost);
        Assert.Equal(122.04m, result.AnnualCost);
        Assert.Equal(new DateTime(2027, 6, 1), result.NextExpectedChargeDate);
        Assert.NotEqual(Guid.Empty, result.PatternId);
        var savedSubscription = Assert.Single(saved!);
        Assert.Equal(result.PatternId, savedSubscription.Id);
        Assert.Equal(122m, savedSubscription.ReferenceAmount);
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

    [Fact]
    public async Task GetRecurringTransactions_GroupsSameMerchantWithinFivePercent()
    {
        var account = Account(
            Entry(1, new DateTime(2026, 4, 15), -100m, "Streaming service"),
            Entry(2, new DateTime(2026, 5, 15), -103m, "Streaming service"),
            Entry(3, new DateTime(2026, 6, 15), -105m, "Streaming service"));
        Setup(account, []);

        var result = Assert.Single(await CreateService().GetRecurringTransactions(
            7,
            TestContext.Current.CancellationToken));

        Assert.Equal(RecurringCadence.Monthly, result.Cadence);
        Assert.Equal(102.67m, result.MonthlyCost);
    }

    [Fact]
    public async Task GetRecurringTransactions_KeepsEveryClusterMemberWithinToleranceOfFinalMedian()
    {
        var account = Account(
            Entry(1, new DateTime(2025, 12, 15), -100m, "Streaming service"),
            Entry(2, new DateTime(2026, 1, 15), -105m, "Streaming service"),
            Entry(3, new DateTime(2026, 2, 15), -105m, "Streaming service"),
            Entry(4, new DateTime(2026, 3, 15), -105m, "Streaming service"),
            Entry(5, new DateTime(2026, 4, 15), -110m, "Streaming service"),
            Entry(6, new DateTime(2026, 5, 15), -110m, "Streaming service"),
            Entry(7, new DateTime(2026, 6, 15), -110m, "Streaming service"),
            Entry(8, new DateTime(2026, 7, 15), -110m, "Streaming service"));
        Setup(account, []);

        var result = Assert.Single(await CreateService().GetRecurringTransactions(
            7,
            TestContext.Current.CancellationToken));

        Assert.Equal(7, result.Entries.Count);
        Assert.Equal(106.43m, result.MonthlyCost);
    }

    [Fact]
    public async Task GetRecurringTransactions_SeparatesMateriallyDifferentAmountsForSameMerchant()
    {
        var account = Account(
            Entry(1, new DateTime(2026, 4, 1), -100m, "Utility provider"),
            Entry(2, new DateTime(2026, 4, 15), -200m, "Utility provider"),
            Entry(3, new DateTime(2026, 5, 1), -101m, "Utility provider"),
            Entry(4, new DateTime(2026, 5, 15), -202m, "Utility provider"),
            Entry(5, new DateTime(2026, 6, 1), -102m, "Utility provider"),
            Entry(6, new DateTime(2026, 6, 15), -204m, "Utility provider"));
        Setup(account, []);

        var result = await CreateService().GetRecurringTransactions(
            7,
            TestContext.Current.CancellationToken);

        Assert.Equal(2, result.Count);
        Assert.All(result, pattern => Assert.Equal(RecurringCadence.Monthly, pattern.Cadence));
        Assert.Equal([101m, 202m], result.Select(x => x.MonthlyCost).Order().ToList());
    }

    [Fact]
    public async Task GetRecurringTransactions_SeparatesInterleavedAnnualPoliciesForSameMerchant()
    {
        var account = Account(
            Entry(1, new DateTime(2024, 9, 1), -610m, "Insurance company"),
            Entry(2, new DateTime(2025, 3, 1), -257m, "Insurance company"),
            Entry(3, new DateTime(2025, 6, 1), -430m, "Insurance company"),
            Entry(4, new DateTime(2025, 9, 1), -610m, "Insurance company"),
            Entry(5, new DateTime(2026, 3, 1), -257m, "Insurance company"),
            Entry(6, new DateTime(2026, 6, 1), -430m, "Insurance company"));
        Setup(account, []);

        var result = await CreateService().GetRecurringTransactions(
            7,
            TestContext.Current.CancellationToken);

        Assert.Equal(3, result.Count);
        Assert.All(result, pattern => Assert.Equal(RecurringCadence.Annual, pattern.Cadence));
        Assert.Equal([257m, 430m, 610m], result.Select(x => x.LastAmount).Order().ToList());
    }

    [Fact]
    public async Task GetRecurringTransactions_MatchesSavedStateByMerchantAndAmount()
    {
        var mutedPatternId = Guid.NewGuid();
        var account = Account(
            Entry(1, new DateTime(2026, 4, 1), -100m, "Utility provider"),
            Entry(2, new DateTime(2026, 4, 15), -200m, "Utility provider"),
            Entry(3, new DateTime(2026, 5, 1), -101m, "Utility provider"),
            Entry(4, new DateTime(2026, 5, 15), -202m, "Utility provider"),
            Entry(5, new DateTime(2026, 6, 1), -102m, "Utility provider"),
            Entry(6, new DateTime(2026, 6, 15), -204m, "Utility provider"));
        Setup(account,
        [
            new RecurringSubscription
            {
                Id = mutedPatternId,
                UserId = 7,
                MerchantKey = "utilityprovider",
                Name = "Utility provider",
                ReferenceAmount = 101m,
                IsMuted = true
            }
        ]);

        var result = await CreateService().GetRecurringTransactions(
            7,
            TestContext.Current.CancellationToken);

        var muted = Assert.Single(result, pattern => pattern.IsMuted);
        Assert.Equal(mutedPatternId, muted.PatternId);
        Assert.Equal(102m, muted.LastAmount);
        Assert.False(Assert.Single(result, pattern => !pattern.IsMuted).IsCancelled);
    }

    [Fact]
    public async Task GetRecurringTransactions_DoesNotTransferLegacyStateWhenMerchantSplits()
    {
        var legacyPatternId = Guid.NewGuid();
        var account = Account(
            Entry(1, new DateTime(2026, 4, 1), -100m, "Utility provider"),
            Entry(2, new DateTime(2026, 4, 15), -200m, "Utility provider"),
            Entry(3, new DateTime(2026, 5, 1), -100m, "Utility provider"),
            Entry(4, new DateTime(2026, 5, 15), -200m, "Utility provider"),
            Entry(5, new DateTime(2026, 6, 1), -100m, "Utility provider"),
            Entry(6, new DateTime(2026, 6, 15), -200m, "Utility provider"));
        Setup(account,
        [
            new RecurringSubscription
            {
                Id = legacyPatternId,
                UserId = 7,
                MerchantKey = "utilityprovider",
                Name = "Utility provider",
                IsMuted = true,
                IsCancelled = true
            }
        ]);

        var result = await CreateService().GetRecurringTransactions(
            7,
            TestContext.Current.CancellationToken);

        Assert.Equal(2, result.Count);
        Assert.DoesNotContain(result, pattern => pattern.PatternId == legacyPatternId);
        Assert.All(result, pattern =>
        {
            Assert.False(pattern.IsMuted);
            Assert.False(pattern.IsCancelled);
        });
    }

    [Fact]
    public async Task GetRecurringCashFlows_DetectsRecurringIncomeWithPositiveAmount()
    {
        var account = Account(
            Entry(1, new DateTime(2026, 4, 15), 500m, "Salary"),
            Entry(2, new DateTime(2026, 5, 15), 500m, "Salary"),
            Entry(3, new DateTime(2026, 6, 15), 500m, "Salary"));
        Setup(account, []);

        var result = Assert.Single(await CreateService().GetRecurringCashFlows(
            7,
            TestContext.Current.CancellationToken));

        Assert.Equal("Salary", result.Name);
        Assert.Equal(500m, result.MonthlyAmount);
        Assert.Equal(500m, result.OccurrenceAmount);
        Assert.Equal(new DateTime(2026, 8, 15), result.NextExpectedDate);
        Assert.NotEqual(Guid.Empty, result.PatternId);
        Assert.False(result.IsMuted);
        Assert.False(result.IsCancelled);
    }

    [Fact]
    public async Task RecurringIncome_WithPaydayAndAmountVariation_IsReturnedByBothQueries()
    {
        var account = Account(
            Entry(1, new DateTime(2026, 5, 30), 4_950m, "ACME payroll"),
            Entry(2, new DateTime(2026, 6, 30), 5_000m, "ACME payroll"),
            Entry(3, new DateTime(2026, 7, 29), 5_050m, "ACME payroll"));
        Setup(account, []);
        var service = CreateService();

        var transaction = Assert.Single(await service.GetRecurringTransactions(
            7,
            TestContext.Current.CancellationToken));
        var cashFlow = Assert.Single(await service.GetRecurringCashFlows(
            7,
            TestContext.Current.CancellationToken));

        Assert.True(transaction.IsIncome);
        Assert.Equal(5_000m, transaction.MonthlyCost);
        Assert.Equal(new DateTime(2026, 8, 29), transaction.NextExpectedChargeDate);
        Assert.Equal(5_000m, cashFlow.MonthlyAmount);
        Assert.Equal(5_050m, cashFlow.OccurrenceAmount);
        Assert.Equal(transaction.NextExpectedChargeDate, cashFlow.NextExpectedDate);
    }

    [Fact]
    public async Task GetRecurringCashFlows_DoesNotPersistNewExpensePatterns()
    {
        var account = Account(
            Entry(1, new DateTime(2026, 4, 15), -40m, "Rent"),
            Entry(2, new DateTime(2026, 5, 15), -40m, "Rent"),
            Entry(3, new DateTime(2026, 6, 15), -40m, "Rent"));
        Setup(account, []);

        _subscriptions.Invocations.Clear();

        var result = Assert.Single(await CreateService().GetRecurringCashFlows(
            7,
            TestContext.Current.CancellationToken));

        Assert.Equal(-40m, result.OccurrenceAmount);
        _subscriptions.Verify(
            x => x.Save(It.IsAny<IEnumerable<RecurringSubscription>>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task GetRecurringCashFlows_DoesNotMutateExistingSubscriptionState()
    {
        var subscription = new RecurringSubscription
        {
            Id = Guid.NewGuid(),
            UserId = 7,
            MerchantKey = "rent",
            Name = "Saved rent name",
            IsMuted = true
        };
        var account = Account(
            Entry(1, new DateTime(2026, 4, 15), -40m, "Rent"),
            Entry(2, new DateTime(2026, 5, 15), -40m, "Rent"),
            Entry(3, new DateTime(2026, 6, 15), -40m, "Rent"));
        Setup(account, [subscription]);

        var result = Assert.Single(await CreateService().GetRecurringCashFlows(
            7,
            TestContext.Current.CancellationToken));

        Assert.Equal("Saved rent name", subscription.Name);
        Assert.True(result.IsMuted);
        _subscriptions.Verify(
            x => x.Save(It.IsAny<IEnumerable<RecurringSubscription>>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task GetRecurringCashFlows_UsesProvidedAsOfDate()
    {
        var account = Account(
            Entry(1, new DateTime(2026, 4, 15), -40m, "Rent"),
            Entry(2, new DateTime(2026, 5, 15), -40m, "Rent"),
            Entry(3, new DateTime(2026, 6, 15), -40m, "Rent"));
        Setup(account, []);

        var result = await CreateService().GetRecurringCashFlows(
            7,
            new DateTime(2026, 8, 15),
            TestContext.Current.CancellationToken);

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetRecurringCashFlows_UsesOnlyCashAccountsWithoutChangingSubscriptionDetection()
    {
        var loan = AccountWithType(
            AccountLabel.Loan,
            Entry(1, new DateTime(2026, 4, 15), -40m, "Loan payment"),
            Entry(2, new DateTime(2026, 5, 15), -40m, "Loan payment"),
            Entry(3, new DateTime(2026, 6, 15), -40m, "Loan payment"));
        Setup([loan], []);
        var service = CreateService();

        Assert.Empty(await service.GetRecurringCashFlows(7, TestContext.Current.CancellationToken));
        Assert.Single(await service.GetRecurringTransactions(7, TestContext.Current.CancellationToken));
    }

    private RecurringTransactionDetectorService CreateService() =>
        new(_accounts.Object, _subscriptions.Object, _clock);

    private void Setup(CurrencyAccount account, List<RecurringSubscription> subscriptions)
    {
        Setup([account], subscriptions);
    }

    private void Setup(IReadOnlyCollection<CurrencyAccount> accounts, List<RecurringSubscription> subscriptions)
    {
        _accounts
            .Setup(x => x.GetAccounts<CurrencyAccount>(
                7,
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                It.IsAny<bool>()))
            .Returns(accounts.ToAsyncEnumerable());
        _subscriptions
            .Setup(x => x.GetAll(7, It.IsAny<CancellationToken>()))
            .ReturnsAsync(subscriptions);
        _subscriptions
            .Setup(x => x.Save(It.IsAny<IEnumerable<RecurringSubscription>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
    }

    private static CurrencyAccount Account(params CurrencyAccountEntry[] entries) =>
        AccountWithType(AccountLabel.Cash, entries);

    private static CurrencyAccount AccountWithType(AccountLabel accountType, params CurrencyAccountEntry[] entries) =>
        new(7, 1, accountType.ToString(), entries, accountType);

    private static CurrencyAccountEntry Entry(int id, DateTime date, decimal amount, string merchant) =>
        new(1, id, date, amount, amount)
        {
            ContractorDetails = merchant,
            Description = merchant
        };
}