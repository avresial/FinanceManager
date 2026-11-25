using FinanceManager.Application.Alerts.Services;
using FinanceManager.Application.TransactionRules;
using FinanceManager.Domain.Labels.Entities;
using FinanceManager.Domain.Labels.Repositories;
using FinanceManager.Domain.TransactionRules;
using FinanceManager.Domain.TransactionRules.Commands;
using FinanceManager.Domain.TransactionRules.Conditions;
using FinanceManager.Domain.TransactionRules.Dtos;
using FinanceManager.Domain.TransactionRules.Entities;
using FinanceManager.Domain.TransactionRules.Models;
using FinanceManager.Domain.TransactionRules.Repositories;
using FinanceManager.Domain.TransactionRules.Services;
using FinanceManager.Domain.Transactions.Cash.Dtos;
using FinanceManager.Domain.Transactions.Cash.Entities;
using FinanceManager.Domain.Transactions.Cash.Repositories;
using FinanceManager.Domain.Transactions.Shared.Dtos;
using FinanceManager.Domain.Transactions.Shared.Repositories;
using Microsoft.Extensions.Logging;
using Moq;

namespace FinanceManager.Tests.Unit.Application.TransactionRules;

[Trait("Category", "Unit")]
public sealed class TransactionRuleServiceTests
{
    private readonly Mock<ITransactionRuleRepository> _repository = new();
    private readonly Mock<IFinancialLabelsRepository> _labels = new();
    private readonly Mock<ICashTransactionReader> _cash = new();
    private readonly Mock<ILogger<TransactionRuleService>> _logger = new();
    private readonly Mock<ITransactionEntryRepository<CashEntry>> _entries = new();
    private readonly Mock<IFinancialAlertService> _alerts = new();
    private readonly TransactionRuleService _service;

    public TransactionRuleServiceTests()
    {
        _labels.Setup(x => x.GetLabels(It.IsAny<CancellationToken>()))
            .Returns(new[] { new FinancialLabel { Id = 1, Name = "Bills" } }.ToAsyncEnumerable());
        _service = new(_repository.Object, new TransactionRuleEngineService(), _labels.Object, _cash.Object, _entries.Object, _alerts.Object, _logger.Object);
    }

    [Fact]
    public async Task CreateRule_AssignsTheNextOrder()
    {
        TransactionRuleDefinition? added = null;
        _repository.Setup(x => x.Add(It.IsAny<TransactionRuleDefinition>(), It.IsAny<CancellationToken>()))
            .Callback<TransactionRuleDefinition, CancellationToken>((rule, _) =>
            {
                added = rule;
                rule.Order = 5;
            })
            .ReturnsAsync((TransactionRuleDefinition rule, CancellationToken _) => rule);

        var result = await _service.CreateRuleAsync(7, new CreateTransactionRule(
            "Bills", [new() { Type = "Contractor", Pattern = "acme" }],
            [new() { Type = "SetLabels", Labels = ["Bills"] }]), TestContext.Current.CancellationToken);

        Assert.Equal(5, result.Order);
        Assert.NotNull(added);
        Assert.Contains("acme", added!.ConditionsJson, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CreateRule_RejectsInvalidAmountComparisonWhenThresholdIsSet()
    {
        var command = new CreateTransactionRule(
            "Invalid amount comparison",
            [new() { Type = "Amount", Threshold = 10m, Comparison = (AmountComparison)999 }],
            [new() { Type = "NormalizeDescription", Value = "Expense" }]);

        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.CreateRuleAsync(7, command, TestContext.Current.CancellationToken));

        Assert.Equal("Amount comparison is invalid.", exception.Message);
        _repository.Verify(x => x.Add(It.IsAny<TransactionRuleDefinition>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Preview_UsesPersistedRulesAndReturnsTheTransformedFacts()
    {
        _repository.Setup(x => x.GetByUserId(7, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<TransactionRuleDefinition>
            {
                new()
                {
                    UserId = 7,
                    Name = "Normalize",
                    Order = 1,
                    ConditionsJson = "[{\"type\":\"Contractor\",\"pattern\":\"acme\"}]",
                    ActionsJson = "[{\"type\":\"NormalizeContractor\",\"value\":\"Acme\"}]"
                }
            });

        var result = await _service.PreviewAsync(7, new("ACME LTD", "Invoice", 15m, TransactionDirection.Expense, []), TestContext.Current.CancellationToken);

        Assert.Equal("Acme", result.FinalFacts.Contractor);
        Assert.Equal(TransactionRuleOutcomeStatus.Applied, Assert.Single(result.RuleOutcomes).Status);
    }

    private static CashLogEntry Cash(long id, int? legacyAccountId = null, decimal change = -10m, string contractor = "ACME", string description = "Invoice") =>
        new(id, new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddDays(-id), change, description, 1, "PLN", contractor, []);

    private void SetupHistory(int userId, params CashLogEntry[] rows) =>
        _cash.Setup(x => x.GetHistory(userId, It.IsAny<OwnerHistoryQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((int _, OwnerHistoryQuery query, CancellationToken _) =>
                [.. rows.Where(row => query.Boundary is not OwnerHistoryBoundary boundary || row.PostingDate < boundary.Date
                    || row.PostingDate == boundary.Date && row.EntryId < boundary.EntryId).Take(query.Take)]);

    private static CreateTransactionRule UnsavedAcmeRule() => new(
        "Unsaved rule",
        [new() { Type = "Contractor", Pattern = "ACME" }],
        [new() { Type = "SetLabels", Labels = ["Bills"] }]);

    [Fact]
    public async Task Test_UsesUnsavedRule_ReturnsAtMostFiveMatches_AndPerformsNoWrites()
    {
        SetupHistory(7, [.. Enumerable.Range(1, 7).Select(index => Cash(index, legacyAccountId: index % 2 == 0 ? 2 : null))]);

        var results = await _service.TestAsync(7, UnsavedAcmeRule(), TestContext.Current.CancellationToken);

        Assert.Equal(5, results.Count);
        Assert.All(results, result =>
        {
            Assert.Equal("PLN", result.CurrencyCode);
            Assert.Empty(result.Before.Labels);
            Assert.Equal(["Bills"], result.After.Labels);
            Assert.True(result.HasChanges);
        });
        Assert.Equal([1L, 2, 3, 4, 5], results.Select(result => result.TransactionId));
        _repository.Verify(x => x.Add(It.IsAny<TransactionRuleDefinition>(), It.IsAny<CancellationToken>()), Times.Never);
        _repository.Verify(x => x.Update(It.IsAny<TransactionRuleDefinition>(), It.IsAny<CancellationToken>()), Times.Never);
        _entries.Verify(x => x.UpdateByOwner(It.IsAny<int>(), It.IsAny<CashEntry>(), It.IsAny<CancellationToken>()), Times.Never);
        _alerts.Verify(x => x.EvaluateAlertsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Test_NegativeGuestUser_ReadsTheGuestsOwnHistory()
    {
        SetupHistory(-4, Cash(1));

        var results = await _service.TestAsync(-4, UnsavedAcmeRule(), TestContext.Current.CancellationToken);

        Assert.Single(results);
        _cash.Verify(x => x.GetHistory(-4, It.IsAny<OwnerHistoryQuery>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Test_PagesNewestFirstUntilEnoughMatchesAreFound()
    {
        // 200 non-matching rows fill the first page, so the matches only appear on the second page.
        var rows = Enumerable.Range(1, 200).Select(index => Cash(index, contractor: "Other"))
            .Concat(Enumerable.Range(201, 3).Select(index => Cash(index))).ToArray();
        SetupHistory(7, rows);

        var results = await _service.TestAsync(7, UnsavedAcmeRule(), TestContext.Current.CancellationToken);

        Assert.Equal([201L, 202, 203], results.Select(result => result.TransactionId));
        _cash.Verify(x => x.GetHistory(7, It.Is<OwnerHistoryQuery>(query => query.Take == 200 && query.Boundary == null), It.IsAny<CancellationToken>()), Times.Once);
        _cash.Verify(x => x.GetHistory(7, It.Is<OwnerHistoryQuery>(query => query.Boundary != null && query.Boundary.EntryId == 200 && !query.Boundary.IncludeEqualId), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Test_UsesCreateValidationBeforeReadingTransactions()
    {
        var command = new CreateTransactionRule(
            "Invalid rule",
            [new() { Type = "Contractor", Pattern = null }],
            [new() { Type = "NormalizeDescription", Value = "Receipt" }]);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.TestAsync(7, command, TestContext.Current.CancellationToken));

        _cash.Verify(x => x.GetHistory(It.IsAny<int>(), It.IsAny<OwnerHistoryQuery>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData("Account")]
    [InlineData(" account ")]
    [InlineData("ACCOUNT")]
    public async Task CreateAndTest_RejectTheRetiredAccountCondition(string type)
    {
        var command = new CreateTransactionRule("Retired", [new() { Type = type }], [new() { Type = "NormalizeDescription", Value = "Receipt" }]);

        var created = await Assert.ThrowsAsync<ArgumentException>(() => _service.CreateRuleAsync(7, command, TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<ArgumentException>(() => _service.TestAsync(7, command, TestContext.Current.CancellationToken));

        Assert.Contains("retired", created.Message, StringComparison.OrdinalIgnoreCase);
        _repository.Verify(x => x.Add(It.IsAny<TransactionRuleDefinition>(), It.IsAny<CancellationToken>()), Times.Never);
        _cash.Verify(x => x.GetHistory(It.IsAny<int>(), It.IsAny<OwnerHistoryQuery>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Test_DisabledRule_ReturnsNoMatchesWithoutReadingTransactions()
    {
        var command = new CreateTransactionRule(
            "Disabled rule",
            [new() { Type = "Contractor", Pattern = "ACME" }],
            [new() { Type = "NormalizeDescription", Value = "Receipt" }],
            IsEnabled: false);

        var results = await _service.TestAsync(7, command, TestContext.Current.CancellationToken);

        Assert.Empty(results);
        _cash.Verify(x => x.GetHistory(It.IsAny<int>(), It.IsAny<OwnerHistoryQuery>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ApplyToEntry_ResolvesKnownLabelsAndUpdatesTheEntry()
    {
        _repository.Setup(x => x.GetByUserId(7, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<TransactionRuleDefinition>
            {
                new()
                {
                    UserId = 7,
                    Name = "Bills",
                    Order = 1,
                    ConditionsJson = "[{\"type\":\"Description\",\"pattern\":\"invoice\"}]",
                    ActionsJson = "[{\"type\":\"SetLabels\",\"labels\":[\"Bills\"]}]"
                }
            });
        var entry = new CashEntry(2, 3, DateTime.UtcNow, -20m, -20m)
        {
            Description = "Invoice from ACME",
            ContractorDetails = "ACME"
        };

        var changed = await _service.ApplyToEntryAsync(7, entry, TestContext.Current.CancellationToken);

        Assert.True(changed);
        Assert.Single(entry.Labels);
        Assert.Equal("Bills", entry.Labels.Single().Name);
        Assert.Equal(1, entry.Labels.Single().Id);
    }

    [Fact]
    public async Task ApplyToEntry_DescriptionOnlyRule_KeepsCaseVariantAttachedLabels()
    {
        SetupSingleRule("[{\"type\":\"NormalizeDescription\",\"value\":\"Groceries\"}]");
        var entry = EntryWithCaseVariantLabels();

        var changed = await _service.ApplyToEntryAsync(7, entry, TestContext.Current.CancellationToken);

        Assert.True(changed);
        Assert.Equal("Groceries", entry.Description);
        Assert.Equal([7, 8], entry.Labels.Select(label => label.Id));
    }

    [Fact]
    public async Task ApplyToEntry_ReplaceLabelsRule_KeepsOnlyTheRequestedCaseVariant()
    {
        SetupSingleRule("[{\"type\":\"SetLabels\",\"labels\":[\"FOOD\"],\"replaceExisting\":true}]");
        var entry = EntryWithCaseVariantLabels();

        var changed = await _service.ApplyToEntryAsync(7, entry, TestContext.Current.CancellationToken);

        Assert.True(changed);
        Assert.Equal(8, Assert.Single(entry.Labels).Id);
    }

    [Fact]
    public async Task ApplyToEntry_ReplaceLabelsRule_RemovesAllCaseVariants()
    {
        SetupSingleRule("[{\"type\":\"SetLabels\",\"labels\":[\"Bills\"],\"replaceExisting\":true}]");
        var entry = EntryWithCaseVariantLabels();

        var changed = await _service.ApplyToEntryAsync(7, entry, TestContext.Current.CancellationToken);

        Assert.True(changed);
        Assert.Equal(1, Assert.Single(entry.Labels).Id);
    }

    private void SetupSingleRule(string actionsJson) =>
        _repository.Setup(x => x.GetByUserId(7, It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                new TransactionRuleDefinition
                {
                    UserId = 7,
                    Name = "Rule",
                    Order = 1,
                    ConditionsJson = "[]",
                    ActionsJson = actionsJson
                }
            ]);

    private static CashEntry EntryWithCaseVariantLabels() =>
        new(2, 3, DateTime.UtcNow, -20m, -20m)
        {
            Description = "Shop",
            Labels = [new FinancialLabel { Id = 7, Name = "Food" }, new FinancialLabel { Id = 8, Name = "FOOD" }]
        };

    [Fact]
    public async Task ApplyToEntry_ReturnsFalseWhenOnlyUnavailableLabelsAreRequested()
    {
        _repository.Setup(x => x.GetByUserId(7, It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                new TransactionRuleDefinition
                {
                    UserId = 7,
                    Name = "Unknown label",
                    Order = 1,
                    ConditionsJson = "[]",
                    ActionsJson = "[{\"type\":\"SetLabels\",\"labels\":[\"Missing\"]}]"
                }
            ]);

        var entry = new CashEntry(2, 3, DateTime.UtcNow, -20m, -20m)
        {
            Description = "Invoice from ACME"
        };

        var changed = await _service.ApplyToEntryAsync(7, entry, TestContext.Current.CancellationToken);

        Assert.False(changed);
        Assert.Empty(entry.Labels);
    }

    [Fact]
    public async Task ApplyRetroactively_RequiresExplicitConfirmation()
    {
        await Assert.ThrowsAsync<ArgumentException>(() => _service.ApplyRetroactivelyAsync(7, new ApplyTransactionRules(false), TestContext.Current.CancellationToken));
    }

    private void SetupInvoiceRule() =>
        _repository.Setup(x => x.GetByUserId(7, It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                new TransactionRuleDefinition
                {
                    UserId = 7,
                    Name = "Normalize descriptions",
                    Order = 1,
                    ConditionsJson = "[{\"type\":\"Description\",\"pattern\":\"invoice\"}]",
                    ActionsJson = "[{\"type\":\"NormalizeDescription\",\"value\":\"Receipt\"}]"
                }
            ]);

    [Fact]
    public async Task ApplyRetroactively_UpdatesLinkedAndUnlinkedEntries_AndReevaluatesAlertsOnce()
    {
        SetupInvoiceRule();
        SetupHistory(7, Cash(1, legacyAccountId: 2), Cash(2));
        var entries = new[]
        {
            new CashEntry(2, 1, DateTime.UtcNow, -10m, -10m) { Description = "Invoice one", UserId = 7 },
            new CashEntry(null, 2, DateTime.UtcNow.AddDays(-1), 0, -20m) { Description = "Invoice two", UserId = 7, CurrencyId = 1 }
        };
        _entries.Setup(x => x.GetByIds(7, It.Is<IReadOnlyCollection<long>>(ids => ids.SequenceEqual(new long[] { 1, 2 })), It.IsAny<CancellationToken>()))
            .ReturnsAsync(entries);
        _entries.Setup(x => x.UpdateByOwner(7, It.IsAny<CashEntry>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _alerts.Setup(x => x.EvaluateAlertsAsync(7, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var result = await _service.ApplyRetroactivelyAsync(7, new ApplyTransactionRules(true), TestContext.Current.CancellationToken);

        Assert.Equal(2, result.Examined);
        Assert.Equal(2, result.Updated);
        Assert.All(entries, entry => Assert.Equal("Receipt", entry.Description));
        _entries.Verify(x => x.UpdateByOwner(7, It.IsAny<CashEntry>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
        _alerts.Verify(x => x.EvaluateAlertsAsync(7, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ApplyRetroactively_PagesThroughAllOwnedEntries()
    {
        SetupInvoiceRule();
        var rows = Enumerable.Range(1, 201).Select(index => Cash(index)).ToArray();
        SetupHistory(7, rows);
        _entries.Setup(x => x.GetByIds(7, It.IsAny<IReadOnlyCollection<long>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((int _, IReadOnlyCollection<long> ids, CancellationToken _) =>
                [.. ids.Select(id => new CashEntry(null, id, DateTime.UtcNow, 0, -1m) { Description = "Other" })]);

        var result = await _service.ApplyRetroactivelyAsync(7, new ApplyTransactionRules(true), TestContext.Current.CancellationToken);

        Assert.Equal(201, result.Examined);
        Assert.Equal(0, result.Updated);
        _entries.Verify(x => x.GetByIds(7, It.IsAny<IReadOnlyCollection<long>>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
        _alerts.Verify(x => x.EvaluateAlertsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ApplyRetroactively_ForOneEntry_UsesTheOwnerReadAndWrite()
    {
        SetupInvoiceRule();
        var entry = new CashEntry(null, 9, DateTime.UtcNow, 0, -10m) { Description = "Invoice", UserId = 7, CurrencyId = 1 };
        _entries.Setup(x => x.GetByOwner(7, 9, It.IsAny<CancellationToken>())).ReturnsAsync(entry);
        _entries.Setup(x => x.UpdateByOwner(7, entry, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var result = await _service.ApplyRetroactivelyAsync(7, new ApplyTransactionRules(true, 9), TestContext.Current.CancellationToken);

        Assert.Equal(1, result.Examined);
        Assert.Equal(1, result.Updated);
        Assert.Equal("Receipt", entry.Description);
        _cash.Verify(x => x.GetHistory(It.IsAny<int>(), It.IsAny<OwnerHistoryQuery>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ApplyRetroactively_ForAForeignOrMissingEntry_ChangesNothing()
    {
        SetupInvoiceRule();
        _entries.Setup(x => x.GetByOwner(7, 9, It.IsAny<CancellationToken>())).ReturnsAsync((CashEntry?)null);

        var result = await _service.ApplyRetroactivelyAsync(7, new ApplyTransactionRules(true, 9), TestContext.Current.CancellationToken);

        Assert.Equal(0, result.Examined);
        _entries.Verify(x => x.UpdateByOwner(It.IsAny<int>(), It.IsAny<CashEntry>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ApplyToEntry_UnlinkedEntry_AppliesRulesThatDoNotNeedAnAccount()
    {
        SetupSingleRule("[{\"type\":\"NormalizeDescription\",\"value\":\"Groceries\"}]");
        var entry = new CashEntry(null, 3, DateTime.UtcNow, 0, -20m) { Description = "Shop", UserId = 7, CurrencyId = 0 };

        var changed = await _service.ApplyToEntryAsync(7, entry, TestContext.Current.CancellationToken);

        Assert.True(changed);
        Assert.Equal("Groceries", entry.Description);
    }

    [Fact]
    public async Task PersistedRuleWithAResidualAccountCondition_NeverMatches_NeverThrows_AndIsReportedOnce()
    {
        var ruleId = Guid.NewGuid();
        // Exotic padding the data migration does not rewrite: the runtime must still never broaden or throw.
        _repository.Setup(x => x.GetByUserId(7, It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                new TransactionRuleDefinition
                {
                    Id = ruleId,
                    UserId = 7,
                    Name = "Account rule",
                    Order = 1,
                    IsEnabled = true,
                    ConditionsJson = "[{\"tYpE\":\"\\u00a0Account\\n\",\"accountIds\":[2]}]",
                    ActionsJson = "[{\"type\":\"NormalizeDescription\",\"value\":\"Matched\"}]"
                }
            ]);
        var first = new CashEntry(2, 3, DateTime.UtcNow, -20m, -20m) { Description = "Shop" };
        var second = new CashEntry(null, 4, DateTime.UtcNow, 0, -20m) { Description = "Shop" };

        Assert.False(await _service.ApplyToEntryAsync(7, first, TestContext.Current.CancellationToken));
        Assert.False(await _service.ApplyToEntryAsync(7, second, TestContext.Current.CancellationToken));
        var preview = await _service.PreviewAsync(7, new("ACME", "Shop", 20m, TransactionDirection.Expense, []), TestContext.Current.CancellationToken);

        Assert.Equal("Shop", first.Description);
        Assert.Equal("Shop", second.Description);
        Assert.Equal(TransactionRuleOutcomeStatus.SkippedDisabled, Assert.Single(preview.RuleOutcomes).Status);
        _logger.Verify(x => x.Log(
            LogLevel.Warning, It.IsAny<EventId>(),
            It.Is<It.IsAnyType>((state, _) => (Convert.ToString(state) ?? string.Empty).Contains(ruleId.ToString())),
            It.IsAny<Exception?>(), It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.Once);
    }
}