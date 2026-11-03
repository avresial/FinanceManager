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

namespace FinanceManager.Components.Features.Alerts.Components;

public partial class AlertsPage : ComponentBase
{
    private readonly List<string> _errors = [];
    private readonly Dictionary<Guid, AlertEvaluationOutcome> _outcomes = [];
    private readonly HashSet<Guid> _pendingAlertIds = [];
    private List<FinancialAlertDto> _alerts = [];
    private AlertFormModel _form = new();
    private int _formVersion;
    private MudTextField<string>? _alertNameField;

    private Guid? _editingId;
    private bool _isLoading = true;
    private bool _isRefreshing;
    private bool _isSaving;
    private bool _isLoadingAccounts;
    private bool _accountOptionsFailed;
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

    private async Task FocusAlertFormAsync()
    {
        if (_alertNameField is not null) await _alertNameField.FocusAsync();
    }

    private async Task LoadFormOptionsAsync()
    {
        if (_isLoadingAccounts) return;
        _isLoadingAccounts = true;
        _accountOptionsFailed = false;
        try
        {
            _accounts = [.. (await CurrencyAccountHttpClient.GetAvailableAccountsAsync()).OrderBy(account => account.AccountName)];
            _currencyCode = (await SettingsService.GetCurrencyAsync()).ShortName;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Unable to load account options for the alert form");
            _accountOptionsFailed = true;
        }
        finally
        {
            _isLoadingAccounts = false;
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

    private async Task RefreshAfterMutationAsync()
    {
        _refreshGate.Claim();
        if (_userId is int userId)
            await SnapshotService.RemoveAsync(SnapshotKey(userId));
        await RefreshAsync();
    }

    private async Task SaveAsync()
    {
        if (_isSaving || (_editingId is Guid pendingId && _pendingAlertIds.Contains(pendingId))) return;
        _errors.Clear();
        if (!_form.HasTitle)
        {
            _errors.Add("Alert name is required.");
            return;
        }

        if (_form.TargetAccountId is not null && (_isLoadingAccounts || _accountOptionsFailed))
        {
            _errors.Add("Load account options before saving this alert.");
            return;
        }

        _isSaving = true;
        try
        {
            if (_editingId is Guid id)
            {
                var updated = await HttpClient.UpdateAsync(id, _form.ToUpdateCommand());
                if (updated is null)
                {
                    _errors.Add("Unable to update this alert.");
                    return;
                }
            }
            else
            {
                var created = await HttpClient.CreateAsync(_form.ToCreateCommand());
                if (created is null)
                {
                    _errors.Add("Unable to create this alert.");
                    return;
                }
            }

            Snackbar.Add(_editingId is null ? "Alert created." : "Alert updated.", Severity.Success);
            ResetForm();
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
        if (_isSaving || !_pendingAlertIds.Add(alert.Id)) return;
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
        finally
        {
            _pendingAlertIds.Remove(alert.Id);
        }
    }

    private async Task DeleteAsync(FinancialAlertDto alert)
    {
        if (_isSaving || !_pendingAlertIds.Add(alert.Id)) return;
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
        finally
        {
            _pendingAlertIds.Remove(alert.Id);
        }
    }

    private void BeginEdit(FinancialAlertDto alert)
    {
        if (_isSaving || _pendingAlertIds.Contains(alert.Id)) return;
        _editingId = alert.Id;
        _form = AlertFormModel.FromDto(alert);
    }

    private void CancelEdit()
    {
        if (_isSaving) return;
        ResetForm();
    }

    private void ResetForm()
    {
        _editingId = null;
        _form = new AlertFormModel();
        _formVersion++;
        _errors.Clear();
    }
}