using FinanceManager.Application.TransactionRules;
using FinanceManager.Domain.TransactionRules;
using FinanceManager.Domain.TransactionRules.Actions;
using FinanceManager.Domain.TransactionRules.Conditions;
using FinanceManager.Domain.TransactionRules.Models;

namespace FinanceManager.Tests.Unit.Application.TransactionRules;

[Trait("Category", "Unit")]
public class TransactionRuleEngineServiceTests
{
    private readonly TransactionRuleEngineService _service = new();

    private static TransactionFacts Facts(
        string contractor = "ACME Corp. - March invoice",
        string description = "Invoice #123",
        int accountId = 10,
        decimal amount = 100m,
        TransactionDirection direction = TransactionDirection.Expense,
        IReadOnlyList<string>? labels = null)
    {
        IReadOnlyList<string> labelList = labels ?? [];
        return new TransactionFacts(contractor, description, accountId, amount, direction, labelList);
    }

    [Fact]
    public void RunRules_ReturnsTheSameResultAsTheDomainEngine()
    {
        var rule = new TransactionRule(Guid.NewGuid(), "Normalize ACME", 1,
            [new ContractorCondition("acme")],
            [new NormalizeContractorAction("Acme"), new SetLabelsAction(["Bills"])]);
        var facts = Facts();

        var viaService = _service.RunRules([rule], facts);
        var viaEngine = TransactionRuleEngine.Run([rule], facts);

        Assert.Equal(viaEngine.FinalFacts, viaService.FinalFacts);
        Assert.Equal(viaEngine.StoppedRuleId, viaService.StoppedRuleId);
        Assert.Equal(viaEngine.HasChanges, viaService.HasChanges);
        Assert.Equal(viaEngine.RuleOutcomes.Select(outcome => outcome.Status), viaService.RuleOutcomes.Select(outcome => outcome.Status));
    }

    [Fact]
    public void RunRules_ProducesAUsableImportPreview()
    {
        var rules = new[]
        {
            new TransactionRule(Guid.NewGuid(), "Tidy ACME", 1,
                [new ContractorCondition("ACME corp")],
                [new NormalizeContractorAction("Acme"), new NormalizeDescriptionAction("Acme - March invoice"), new SetLabelsAction(["Bills"])]),
            new TransactionRule(Guid.NewGuid(), "Never matches", 2,
                [new ContractorCondition("zzz")],
                [new SetLabelsAction(["Nope"])]),
        };

        var result = _service.RunRules(rules, Facts(contractor: "ACME corp. - March", labels: ["Old"]));

        Assert.True(result.HasChanges);
        Assert.Equal("Acme", result.FinalFacts.Contractor);
        Assert.Equal("Acme - March invoice", result.FinalFacts.Description);
        IReadOnlyList<string> expected = ["Old", "Bills"];
        Assert.Equal(expected, result.FinalFacts.Labels);
        Assert.Single(result.AppliedRules);
    }

    [Fact]
    public void RunRules_DoesNotMutateTheInputFacts()
    {
        var facts = Facts(labels: ["Existing"]);
        var rule = new TransactionRule(Guid.NewGuid(), "r", 1,
            [new ContractorCondition("acme")],
            [new NormalizeContractorAction("New"), new SetLabelsAction(["Added"], replaceExisting: true)]);

        var result = _service.RunRules([rule], facts);

        Assert.True(result.HasChanges);
        Assert.Equal("ACME Corp. - March invoice", facts.Contractor);
        Assert.Equal("Invoice #123", facts.Description);
        IReadOnlyList<string> expected = ["Existing"];
        Assert.Equal(expected, facts.Labels);
    }

    [Fact]
    public void RunRules_IsDeterministicForTheSameInput()
    {
        var rules = new[]
        {
            new TransactionRule(Guid.NewGuid(), "r1", 1,
                [new ContractorCondition("acme")],
                [new NormalizeDescriptionAction("Normalized"), new SetLabelsAction(["A"])]),
        };
        var facts = Facts();

        var first = _service.RunRules(rules, facts);
        var second = _service.RunRules(rules, facts);

        Assert.Equal(first.FinalFacts, second.FinalFacts);
        Assert.Equal(first.HasChanges, second.HasChanges);
        Assert.Equal(first.RuleOutcomes.Select(outcome => outcome.Description), second.RuleOutcomes.Select(outcome => outcome.Description));
    }

    [Fact]
    public void RunRules_WithNoRules_ReturnsTheFactsUnchanged()
    {
        var facts = Facts();

        var result = _service.RunRules([], facts);

        Assert.False(result.HasChanges);
        Assert.Equal(facts, result.FinalFacts);
    }
}