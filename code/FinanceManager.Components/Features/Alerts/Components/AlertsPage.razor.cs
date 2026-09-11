using FinanceManager.Application.Alerts.Models;
using FinanceManager.Components.Features.Alerts.HttpClients;
using FinanceManager.Domain.Alerts.Commands;
using FinanceManager.Domain.Alerts.Dtos;
using FinanceManager.Domain.Alerts.Enums;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using MudBlazor;

namespace FinanceManager.Components.Features.Alerts.Components;

public partial class AlertsPage : ComponentBase
{
    private readonly List<string> _errors = [];
    private readonly Dictionary<Guid, AlertEvaluationOutcome> _outcomes = [];
    private List<FinancialAlertDto> _alerts = [];
    private AlertFormModel _form = new();
    private Guid? _editingId;
    private bool _isLoading = true;
    private bool _isSaving;

    [Inject] public required FinancialAlertsHttpClient HttpClient { get; set; }
    [Inject] public required ILogger<AlertsPage> Logger { get; set; }
    [Inject] public required ISnackbar Snackbar { get; set; }

    protected override Task OnInitializedAsync() => RefreshAsync();

    private async Task RefreshAsync()
    {
        _isLoading = true;
        _errors.Clear();

        try
        {
            var outcomes = await HttpClient.EvaluateAsync();
            _outcomes.Clear();
            foreach (var outcome in outcomes)
                _outcomes[outcome.AlertId] = outcome;

            _alerts = await HttpClient.GetAsync();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Unable to load financial alerts");
            _errors.Add("Unable to load alerts. Please try again.");
        }
        finally
        {
            _isLoading = false;
        }
    }

    private async Task SaveAsync()
    {
        _errors.Clear();
        if (string.IsNullOrWhiteSpace(_form.Title))
        {
            _errors.Add("Alert name is required.");
            return;
        }

        int? accountId = null;
        if (!string.IsNullOrWhiteSpace(_form.AccountIdText))
        {
            if (!int.TryParse(_form.AccountIdText, out var parsedAccountId) || parsedAccountId <= 0)
            {
                _errors.Add("Account id must be a positive number.");
                return;
            }

            accountId = parsedAccountId;
        }

        _isSaving = true;
        try
        {
            TimeSpan? cooldown = _form.CooldownHours <= 0 ? null : TimeSpan.FromHours(_form.CooldownHours);
            if (_editingId is Guid id)
            {
                var updated = await HttpClient.UpdateAsync(
                    id,
                    new UpdateFinancialAlert(
                        _form.Title.Trim(),
                        _form.IsEnabled,
                        _form.ComparisonOperator,
                        _form.Threshold,
                        _form.EvaluationPeriod,
                        accountId,
                        null,
                        NullIfWhiteSpace(_form.LabelName),
                        NullIfWhiteSpace(_form.MerchantName),
                        null,
                        cooldown,
                        _form.AlertType));
                if (updated is null)
                {
                    _errors.Add("Unable to update this alert.");
                    return;
                }
            }
            else
            {
                var created = await HttpClient.CreateAsync(
                    new CreateFinancialAlert(
                        _form.Title.Trim(),
                        _form.AlertType,
                        _form.ComparisonOperator,
                        _form.Threshold,
                        _form.EvaluationPeriod,
                        accountId,
                        null,
                        NullIfWhiteSpace(_form.LabelName),
                        NullIfWhiteSpace(_form.MerchantName),
                        null,
                        cooldown));
                if (created is null)
                {
                    _errors.Add("Unable to create this alert.");
                    return;
                }
            }

            Snackbar.Add(_editingId is null ? "Alert created." : "Alert updated.", Severity.Success);
            CancelEdit();
            await RefreshAsync();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Unable to save financial alert");
            _errors.Add("Unable to save this alert. Please try again.");
        }
        finally
        {
            _isSaving = false;
        }
    }

    private async Task ToggleEnabledAsync(FinancialAlertDto alert, bool enabled)
    {
        try
        {
            if (!await HttpClient.SetEnabledAsync(alert.Id, enabled))
            {
                Snackbar.Add("Unable to update this alert.", Severity.Error);
                return;
            }

            await RefreshAsync();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Unable to toggle financial alert {AlertId}", alert.Id);
            Snackbar.Add("Unable to update this alert.", Severity.Error);
        }
    }

    private async Task DeleteAsync(FinancialAlertDto alert)
    {
        try
        {
            if (!await HttpClient.DeleteAsync(alert.Id))
            {
                Snackbar.Add("Unable to delete this alert.", Severity.Error);
                return;
            }

            if (_editingId == alert.Id)
                CancelEdit();
            Snackbar.Add("Alert deleted.", Severity.Success);
            await RefreshAsync();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Unable to delete financial alert {AlertId}", alert.Id);
            Snackbar.Add("Unable to delete this alert.", Severity.Error);
        }
    }

    private void BeginEdit(FinancialAlertDto alert)
    {
        _editingId = alert.Id;
        _form = new AlertFormModel
        {
            Title = alert.Title,
            AlertType = alert.AlertType,
            IsEnabled = alert.IsEnabled,
            ComparisonOperator = alert.ComparisonOperator,
            Threshold = alert.Threshold,
            EvaluationPeriod = alert.EvaluationPeriod,
            AccountIdText = alert.AccountId?.ToString() ?? string.Empty,
            LabelName = alert.LabelName ?? string.Empty,
            MerchantName = alert.MerchantName ?? string.Empty,
            CooldownHours = alert.CooldownPeriod?.TotalHours ?? 0,
        };
    }

    private void CancelEdit()
    {
        _editingId = null;
        _form = new AlertFormModel();
        _errors.Clear();
    }

    private static string? NullIfWhiteSpace(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string TypeLabel(AlertType type) => type switch
    {
        AlertType.AccountBalance => "Account balance",
        AlertType.CategorySpending => "Category spending",
        AlertType.MerchantSpending => "Merchant spending",
        AlertType.LargeTransaction => "Large transaction",
        AlertType.SubscriptionPriceChange => "Subscription price change",
        _ => type.ToString()
    };

    private static string ComparisonLabel(AlertComparisonOperator comparison) => comparison switch
    {
        AlertComparisonOperator.GreaterThan => ">",
        AlertComparisonOperator.GreaterThanOrEqual => "≥",
        AlertComparisonOperator.LessThan => "<",
        AlertComparisonOperator.LessThanOrEqual => "≤",
        AlertComparisonOperator.Equal => "=",
        AlertComparisonOperator.NotEqual => "≠",
        _ => comparison.ToString()
    };

    private static string PeriodLabel(AlertEvaluationPeriod period) => period switch
    {
        AlertEvaluationPeriod.CurrentMonth => "Current month",
        AlertEvaluationPeriod.Last30Days => "Last 30 days",
        AlertEvaluationPeriod.Last7Days => "Last 7 days",
        AlertEvaluationPeriod.AllTime => "All time",
        _ => period.ToString()
    };

    private static string GetConditionLabel(FinancialAlertDto alert) =>
        $"{ComparisonLabel(alert.ComparisonOperator)} {alert.Threshold:N2}";

    private string StatusLabel(FinancialAlertDto alert) =>
        _outcomes.TryGetValue(alert.Id, out var outcome)
            ? outcome.Status == AlertTriggerStatus.Triggered ? "Triggered" : "Healthy"
            : alert.LastStatus == AlertTriggerStatus.Triggered ? "Triggered" : "Healthy";

    private Color StatusColor(FinancialAlertDto alert) =>
        StatusLabel(alert) == "Triggered" ? Color.Warning : Color.Success;

    private sealed class AlertFormModel
    {
        public string Title { get; set; } = string.Empty;
        public AlertType AlertType { get; set; } = AlertType.AccountBalance;
        public bool IsEnabled { get; set; } = true;
        public AlertComparisonOperator ComparisonOperator { get; set; } = AlertComparisonOperator.GreaterThan;
        public decimal Threshold { get; set; }
        public AlertEvaluationPeriod EvaluationPeriod { get; set; } = AlertEvaluationPeriod.CurrentMonth;
        public string AccountIdText { get; set; } = string.Empty;
        public string LabelName { get; set; } = string.Empty;
        public string MerchantName { get; set; } = string.Empty;
        public double CooldownHours { get; set; }
    }
}