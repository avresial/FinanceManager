using FinanceManager.Application.TransactionRules;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.FinancialAccounts.Currencies.Repositories;
using FinanceManager.Domain.FinancialAccounts.Shared.Entities;
using FinanceManager.Domain.FinancialAccounts.Shared.Repositories;
using FinanceManager.Domain.Labels.Repositories;
using FinanceManager.Domain.TransactionRules;
using FinanceManager.Domain.TransactionRules.Commands;
using FinanceManager.Domain.TransactionRules.Dtos;
using FinanceManager.Domain.TransactionRules.Entities;
using FinanceManager.Domain.TransactionRules.Models;
using FinanceManager.Domain.TransactionRules.Repositories;
using FinanceManager.Domain.TransactionRules.Services;
using Moq;

namespace FinanceManager.Tests.Unit.Application.TransactionRules;

[Trait("Category", "Unit")]
public sealed class TransactionRuleServiceTests
{
    private readonly Mock<ITransactionRuleRepository> _repository = new();
    private readonly Mock<IFinancialLabelsRepository> _labels = new();
    private readonly Mock<ICurrencyAccountRepository<CurrencyAccount>> _accounts = new();
    private readonly Mock<IAccountEntryRepository<CurrencyAccountEntry>> _entries = new();
    private readonly TransactionRuleService _service;

    public TransactionRuleServiceTests()
    {
        _labels.Setup(x => x.GetLabels(It.IsAny<CancellationToken>()))
            .Returns(new[] { new FinancialLabel { Id = 1, Name = "Bills" } }.ToAsyncEnumerable());
        _service = new(_repository.Object, new TransactionRuleEngineService(), _labels.Object, _accounts.Object, _entries.Object);
    }

    [Fact]
    public async Task CreateRule_AssignsTheNextOrder()
    {
        _repository.Setup(x => x.GetByUserId(7, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<TransactionRuleDefinition>
            {
                new() { UserId = 7, Order = 4 }
            });

        TransactionRuleDefinition? added = null;
        _repository.Setup(x => x.Add(It.IsAny<TransactionRuleDefinition>(), It.IsAny<CancellationToken>()))
            .Callback<TransactionRuleDefinition, CancellationToken>((rule, _) => added = rule)
            .ReturnsAsync((TransactionRuleDefinition rule, CancellationToken _) => rule);

        var result = await _service.CreateRuleAsync(7, new CreateTransactionRule(
            "Bills", [new() { Type = "Contractor", Pattern = "acme" }],
            [new() { Type = "SetLabels", Labels = ["Bills"] }]), TestContext.Current.CancellationToken);

        Assert.Equal(5, result.Order);
        Assert.NotNull(added);
        Assert.Contains("acme", added!.ConditionsJson, StringComparison.OrdinalIgnoreCase);
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

        var result = await _service.PreviewAsync(7, new("ACME LTD", "Invoice", 12, 15m, TransactionDirection.Expense, []), TestContext.Current.CancellationToken);

        Assert.Equal("Acme", result.FinalFacts.Contractor);
        Assert.Equal(TransactionRuleOutcomeStatus.Applied, Assert.Single(result.RuleOutcomes).Status);
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
        var entry = new CurrencyAccountEntry(2, 3, DateTime.UtcNow, -20m, -20m)
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
    public async Task ApplyRetroactively_RequiresExplicitConfirmation()
    {
        await Assert.ThrowsAsync<ArgumentException>(() => _service.ApplyRetroactivelyAsync(7, new ApplyTransactionRules(false), TestContext.Current.CancellationToken));
    }
}