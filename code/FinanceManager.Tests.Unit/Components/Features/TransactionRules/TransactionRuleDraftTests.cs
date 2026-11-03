using FinanceManager.Components.Features.TransactionRules.Models;
using FinanceManager.Domain.TransactionRules.Dtos;

namespace FinanceManager.Tests.Unit.Components.Features.TransactionRules;

[Trait("Category", "Unit")]
public class TransactionRuleDraftTests
{
    [Fact]
    public void LoadFrom_RuleWithoutConditionsOrActions_KeepsOneEditableItemEach()
    {
        var draft = new TransactionRuleDraft();
        var rule = Rule() with { IsEnabled = false, StopProcessing = true };

        draft.LoadFrom(rule);

        Assert.Equal(rule.Id, draft.EditingId);
        Assert.False(draft.Enabled);
        Assert.True(draft.StopProcessing);
        Assert.Single(draft.Conditions);
        Assert.Single(draft.Actions);
    }

    [Fact]
    public void RemoveCondition_KeepsTheLastOne()
    {
        var draft = new TransactionRuleDraft();
        var only = draft.Conditions[0];

        Assert.False(draft.RemoveCondition(only));
        draft.AddCondition();
        Assert.True(draft.RemoveCondition(only));
        Assert.Single(draft.Conditions);
    }

    [Fact]
    public void ToCreateCommand_TrimsNameAndParsesLabels()
    {
        var draft = new TransactionRuleDraft { Name = "  Groceries  " };
        draft.Actions[0].LabelsText = "food, home ,";

        var command = draft.ToCreateCommand();

        Assert.Equal("Groceries", command.Name);
        Assert.Equal(["food", "home"], Assert.Single(command.Actions).Labels);
    }

    [Fact]
    public void Reset_ClearsEditingState()
    {
        var draft = new TransactionRuleDraft();
        draft.LoadFrom(Rule());

        draft.Reset();

        Assert.Null(draft.EditingId);
        Assert.False(draft.HasName);
        Assert.True(draft.Enabled);
    }

    private static TransactionRuleDto Rule() =>
        new(Guid.NewGuid(), "Rule", 0, true, false, [], [], DateTime.UtcNow, null);
}