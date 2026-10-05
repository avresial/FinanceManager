using FinanceManager.Application.Alerts.Models;
using FinanceManager.Components.Features.Alerts.HttpClients;
using FinanceManager.Components.Features.Alerts.Models;
using FinanceManager.Components.Features.FinancialAccounts.HttpClients;
using FinanceManager.Components.Shared.Helpers;
using FinanceManager.Components.Shared.Services;
using FinanceManager.Domain.Alerts.Commands;
using FinanceManager.Domain.Alerts.Dtos;
using FinanceManager.Domain.Alerts.Enums;
using FinanceManager.Domain.FinancialAccounts.Shared.ValueObjects;
using FinanceManager.Domain.Identity.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using MudBlazor;
using System.Text.Json;

namespace FinanceManager.Components.Features.Alerts.Components;

public partial class AlertsPage : ComponentBase
{
    private readonly List<string> _errors = [];
    private readonly Dictionary<Guid, AlertEvaluationOutcome> _outcomes = [];
    private List<FinancialAlertDto> _alerts = [];
    private AlertFormModel _form = new();
    private Guid? _editingId;
    private bool _isLoading = true;
    private bool _isRefreshing;
    private bool _isSaving;
    private int? _userId;
    private List<AvailableAccount> _accounts = [];
    private string _currencyCode = string.Empty;
    private readonly RefreshVersionGate _refreshGate = new();

    [Inject] public required FinancialAlertsHttpClient HttpClient { get; set; }
    [Inject] public required ILogger<AlertsPage> Logger { get; set; }
    [Inject] public required ISnackbar Snackbar { get; set; }
    [Inject] public required ILoginService LoginService { get; set; }
    [Inject] public required ISnapshotRefreshCoordinator SnapshotRefreshCoordinator { get; set; }
    [Inject] public required ISnapshotService SnapshotService { get; set; }
    [Inject] public required CurrencyAccountHttpClient CurrencyAccountHttpClient { get; set; }
    [Inject] public required ISettingsService SettingsService { get; set; }

    protected override async Task OnInitializedAsync()
    {
        _currencyCode = SettingsService.GetCurrency().ShortName;
        var refresh = RefreshAsync();
        await Task.WhenAll(refresh, LoadFormOptionsAsync());
    }

    private async Task LoadFormOptionsAsync()
    {
        try
        {
            _accounts = [.. (await CurrencyAccountHttpClient.GetAvailableAccountsAsync()).OrderBy(account => account.AccountName)];
            _currencyCode = (await SettingsService.GetCurrencyAsync()).ShortName;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Unable to load account options for the alert form");
        }

        await InvokeAsync(StateHasChanged);
    }

    private string AccountLabel(AvailableAccount account) =>
        _accounts.Count(candidate => candidate.AccountName.Equals(account.AccountName, StringComparison.OrdinalIgnoreCase)) > 1
            ? $"{account.AccountName} (#{account.AccountId})"
            : account.AccountName;

    private async Task RefreshAsync()
    {
        var version = _refreshGate.Claim();
        _isRefreshing = true;
        _errors.Clear();
        try
        {
            var user = await LoginService.GetLoggedUser();
            if (!_refreshGate.IsCurrent(version)) return;
            if (user is null)
            {
                _isLoading = false;
                _isRefreshing = false;
                _errors.Add("Unable to load alerts. Please sign in.");
                return;
            }

            if (_userId is int previousUserId && previousUserId != user.UserId)
            {
                _alerts = [];
                _outcomes.Clear();
                _isLoading = true;
            }
            _userId = user.UserId;
            var result = await SnapshotRefreshCoordinator.RunAsync(new SnapshotRefreshRequest<AlertsPageSnapshot, AlertsPageModel>
            {
                Key = SnapshotKey(user.UserId),
                Gate = _refreshGate,
                ClaimedVersion = version,
                ToModel = snapshot => snapshot.UserId == user.UserId
                    && snapshot.Alerts is not null
                    && snapshot.Outcomes is not null
                    ? new AlertsPageModel(snapshot.Alerts, snapshot.Outcomes)
                    : null,
                FetchAsync = FetchAlertsAsync,
                ContentComparer = AlertsPageContentComparer.Instance,
                ToSnapshot = model => new AlertsPageSnapshot
                {
                    UserId = user.UserId,
                    Alerts = model.Alerts,
                    Outcomes = model.Outcomes,
                },
                OnSnapshotPainted = ShowAlertsAsync,
                OnSnapshotMissing = () =>
                {
                    _isLoading = true;
                    return InvokeAsync(StateHasChanged);
                },
                OnRefreshed = ShowAlertsAsync,
            });

            if (!_refreshGate.IsCurrent(version)) return;
            if (result.Error is not null)
            {
                Logger.LogError(result.Error, "Unable to refresh financial alerts");
                _errors.Add("Unable to refresh alerts. Showing the last known status.");
            }
            else if (result.Model is { } model && model.Outcomes.Any(outcome => outcome.Status == AlertTriggerStatus.Error))
            {
                _errors.Add("Unable to evaluate one or more alerts. Showing the last known status.");
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Unable to load financial alerts");
            _errors.Add("Unable to load alerts. Please try again.");
        }
        finally
        {
            if (_refreshGate.IsCurrent(version))
            {
                _isLoading = false;
                _isRefreshing = false;
                StateHasChanged();
            }
        }
    }

    private Task ShowAlertsAsync(AlertsPageModel model)
    {
        _alerts = model.Alerts;
        _outcomes.Clear();
        foreach (var outcome in model.Outcomes)
            _outcomes[outcome.AlertId] = outcome;
        _isLoading = false;
        return InvokeAsync(StateHasChanged);
    }

    private static string SnapshotKey(int userId) => $"alerts-page:{userId}";

    private async Task<AlertsPageModel?> FetchAlertsAsync()
    {
        var alerts = await HttpClient.GetAsync();
        var outcomes = await HttpClient.EvaluateAsync();
        if (alerts.Any(alert => outcomes.All(outcome => outcome.AlertId != alert.Id)))
            throw new InvalidOperationException("Alert evaluation returned incomplete results.");

        return new AlertsPageModel(alerts, outcomes);
    }

    private sealed class AlertsPageContentComparer : IEqualityComparer<AlertsPageModel>
    {
        public static readonly AlertsPageContentComparer Instance = new();

        public bool Equals(AlertsPageModel? x, AlertsPageModel? y) =>
            ReferenceEquals(x, y) || x is not null && y is not null && RenderedContent(x) == RenderedContent(y);

        public int GetHashCode(AlertsPageModel model) => RenderedContent(model).GetHashCode(StringComparison.Ordinal);

        private static string RenderedContent(AlertsPageModel model) => JsonSerializer.Serialize(new
        {
            model.Alerts,
            Outcomes = model.Outcomes.Select(outcome => new
            {
                outcome.AlertId,
                outcome.Status,
                outcome.IsTriggered,
                outcome.CurrentValue,
                outcome.Message,
                outcome.MatchingTransactions,
                outcome.MatchingTransactionCount,
            })
        });
    }

    private async Task RefreshAfterMutationAsync()
    {
        _refreshGate.Claim();
        if (_userId is int userId)
            await SnapshotService.RemoveAsync(SnapshotKey(userId));
        await RefreshAsync();
    }

    private async Task SaveAsync()
    {
        _errors.Clear();
        if (string.IsNullOrWhiteSpace(_form.Title))
        {
            _errors.Add("Alert name is required.");
            return;
        }

        var accountId = _form.AlertType == AlertType.AccountBalance ? _form.AccountId : null;

        var labelName = _form.AlertType == AlertType.CategorySpending ? NullIfWhiteSpace(_form.LabelName) : null;
        var merchantName = _form.AlertType == AlertType.MerchantSpending ? NullIfWhiteSpace(_form.MerchantName) : null;

        _isSaving = true;
        try
        {
            if (_editingId is Guid id)
            {
                var updated = await HttpClient.UpdateAsync(
                    id,
                    new UpdateFinancialAlert(
                        _form.Title.Trim(),
                        _form.AlertType,
                        _form.IsEnabled,
                        _form.ComparisonOperator,
                        _form.Threshold,
                        _form.EvaluationPeriod,
                        accountId,
                        null,
                        labelName,
                        merchantName,
                        null));
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
                        labelName,
                        merchantName,
                        null));
                if (created is null)
                {
                    _errors.Add("Unable to create this alert.");
                    return;
                }
            }

            Snackbar.Add(_editingId is null ? "Alert created." : "Alert updated.", Severity.Success);
            CancelEdit();
            await RefreshAfterMutationAsync();
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

            await RefreshAfterMutationAsync();
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
            await RefreshAfterMutationAsync();
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
            AccountId = alert.AccountId,
            LabelName = alert.LabelName ?? string.Empty,
            MerchantName = alert.MerchantName ?? string.Empty,
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
        $"{ComparisonLabel(alert.ComparisonOperator)} {MoneyFormatter.FormatNumber(alert.Threshold)}";

    private string StatusLabel(FinancialAlertDto alert) =>
        _outcomes.TryGetValue(alert.Id, out var outcome)
            ? outcome.Status switch
            {
                AlertTriggerStatus.Triggered => "Triggered",
                AlertTriggerStatus.Error => "Error",
                _ => "Healthy"
            }
            : alert.LastStatus == AlertTriggerStatus.Triggered ? "Triggered" : "Healthy";

    private Color StatusColor(FinancialAlertDto alert) =>
        StatusLabel(alert) switch
        {
            "Triggered" => Color.Warning,
            "Error" => Color.Error,
            _ => Color.Success
        };

    private sealed class AlertFormModel
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
    }
}