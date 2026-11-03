using FinanceManager.Domain.Alerts.Commands;
using FinanceManager.Domain.Alerts.Dtos;
using FinanceManager.Domain.Alerts.Enums;

namespace FinanceManager.Components.Features.Alerts.Models;

/// <summary>
/// Editable alert form. Only the target field that belongs to the selected <see cref="AlertType"/>
/// (account, label or merchant) is sent; the others are kept in the form but dropped on save.
/// </summary>
internal sealed class AlertFormModel
{
    public string Title { get; set; } = string.Empty;
    public AlertType AlertType { get; set; } = AlertType.AccountBalance;
    public bool IsEnabled { get; set; } = true;
    public AlertComparisonOperator ComparisonOperator { get; set; } = AlertComparisonOperator.GreaterThan;
    public decimal Threshold { get; set; }
    public AlertEvaluationPeriod EvaluationPeriod { get; set; } = AlertEvaluationPeriod.CurrentMonth;
    public int? AccountId { get; set; }
    public string LabelName { get; set; } = string.Empty;
    public string MerchantName { get; set; } = string.Empty;

    public bool HasTitle => !string.IsNullOrWhiteSpace(Title);

    /// <summary>The account to save, or null when the alert type has no account target.</summary>
    public int? TargetAccountId => AlertType == AlertType.AccountBalance ? AccountId : null;

    public static AlertFormModel FromDto(FinancialAlertDto alert) => new()
    {
        Title = alert.Title,
        AlertType = alert.AlertType,
        IsEnabled = alert.IsEnabled,
        ComparisonOperator = alert.ComparisonOperator,
        Threshold = alert.Threshold,
        EvaluationPeriod = alert.EvaluationPeriod,
        AccountId = alert.AccountId,
        LabelName = alert.LabelName ?? string.Empty,
        MerchantName = alert.MerchantName ?? string.Empty,
    };

    public CreateFinancialAlert ToCreateCommand() => new(
        Title.Trim(),
        AlertType,
        ComparisonOperator,
        Threshold,
        EvaluationPeriod,
        TargetAccountId,
        null,
        TargetLabelName,
        TargetMerchantName,
        null);

    public UpdateFinancialAlert ToUpdateCommand() => new(
        Title.Trim(),
        AlertType,
        IsEnabled,
        ComparisonOperator,
        Threshold,
        EvaluationPeriod,
        TargetAccountId,
        null,
        TargetLabelName,
        TargetMerchantName,
        null);

    private string? TargetLabelName => AlertType == AlertType.CategorySpending ? NullIfWhiteSpace(LabelName) : null;

    private string? TargetMerchantName => AlertType == AlertType.MerchantSpending ? NullIfWhiteSpace(MerchantName) : null;

    private static string? NullIfWhiteSpace(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}