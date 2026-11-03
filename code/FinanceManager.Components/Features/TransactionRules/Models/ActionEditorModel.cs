using FinanceManager.Domain.TransactionRules.Dtos;

namespace FinanceManager.Components.Features.TransactionRules.Models;

/// <summary>Mutable form model for one rule action; labels are edited as comma-separated text.</summary>
internal sealed class ActionEditorModel
{
    public string Type { get; set; } = "SetLabels";
    public string? Value { get; set; }
    public string LabelsText { get; set; } = string.Empty;
    public bool ReplaceExisting { get; set; }

    public static ActionEditorModel FromDto(TransactionRuleActionDto source) => new()
    {
        Type = source.Type,
        Value = source.Value,
        LabelsText = string.Join(",", source.Labels),
        ReplaceExisting = source.ReplaceExisting
    };

    public TransactionRuleActionDto ToDto() => new()
    {
        Type = Type,
        Value = Value,
        Labels = Type.Equals("SetLabels", StringComparison.OrdinalIgnoreCase) ? ParseLabels(LabelsText) : [],
        ReplaceExisting = ReplaceExisting
    };

    private static List<string> ParseLabels(string value) => value
        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .ToList();
}