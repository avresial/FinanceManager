using FinanceManager.Domain.TransactionRules.Commands;
using FinanceManager.Domain.TransactionRules.Dtos;

namespace FinanceManager.Components.Features.TransactionRules.Models;

/// <summary>
/// The rule currently being created or edited. Always holds at least one condition and one action
/// so the editor never renders an empty list.
/// </summary>
internal sealed class TransactionRuleDraft
{
    public Guid? EditingId { get; private set; }
    public string Name { get; set; } = string.Empty;
    public bool Enabled { get; set; } = true;
    public bool StopProcessing { get; set; }
    public List<ConditionEditorModel> Conditions { get; private set; } = [new()];
    public List<ActionEditorModel> Actions { get; private set; } = [new()];

    public bool HasName => !string.IsNullOrWhiteSpace(Name);

    public void Reset()
    {
        EditingId = null;
        Name = string.Empty;
        Enabled = true;
        StopProcessing = false;
        Conditions = [new()];
        Actions = [new()];
    }

    public void LoadFrom(TransactionRuleDto rule)
    {
        EditingId = rule.Id;
        Name = rule.Name;
        Enabled = rule.IsEnabled;
        StopProcessing = rule.StopProcessing;
        Conditions = rule.Conditions.Select(ConditionEditorModel.FromDto).ToList();
        Actions = rule.Actions.Select(ActionEditorModel.FromDto).ToList();
        if (Conditions.Count == 0)
            Conditions.Add(new());
        if (Actions.Count == 0)
            Actions.Add(new());
    }

    public void AddCondition() => Conditions.Add(new());

    /// <summary>Returns false when <paramref name="condition"/> is the last one and was kept.</summary>
    public bool RemoveCondition(ConditionEditorModel condition) => Conditions.Count > 1 && Conditions.Remove(condition);

    public void AddAction() => Actions.Add(new());

    /// <summary>Returns false when <paramref name="action"/> is the last one and was kept.</summary>
    public bool RemoveAction(ActionEditorModel action) => Actions.Count > 1 && Actions.Remove(action);

    public CreateTransactionRule ToCreateCommand() =>
        new(Name.Trim(), ConditionDtos(), ActionDtos(), Enabled, StopProcessing);

    public UpdateTransactionRule ToUpdateCommand() =>
        new(Name.Trim(), ConditionDtos(), ActionDtos(), Enabled, StopProcessing);

    private List<TransactionRuleConditionDto> ConditionDtos() => Conditions.Select(condition => condition.ToDto()).ToList();

    private List<TransactionRuleActionDto> ActionDtos() => Actions.Select(action => action.ToDto()).ToList();
}