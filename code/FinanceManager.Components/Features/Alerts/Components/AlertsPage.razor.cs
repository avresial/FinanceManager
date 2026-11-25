using FinanceManager.Application.Alerts.Models;
using FinanceManager.Components.Features.Alerts.HttpClients;
using FinanceManager.Components.Features.Alerts.Models;
using FinanceManager.Components.Features.Currencies.HttpClients;
using FinanceManager.Components.Features.MoneyFlow.HttpClients;
using FinanceManager.Components.Shared.Helpers;
using FinanceManager.Components.Shared.Services;
using FinanceManager.Domain.Alerts.Commands;
using FinanceManager.Domain.Alerts.Dtos;
using FinanceManager.Domain.Alerts.Enums;
using FinanceManager.Domain.Currencies.Entities;
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
    private bool _isLoadingCurrencies;
    private bool _currencyOptionsFailed;
    private int? _userId;
    // The catalog resolves codes and tells whether a stored id exists; the options are what the form offers.
    private List<Currency> _currencies = [];
    private List<Currency> _currencyOptions = [];
    private IReadOnlySet<int>? _knownCurrencyIds;
    private IReadOnlyList<string> _ownedCurrencyCodes = [];
    private bool _editingWasDisabled;
    private bool _editingNeedsCurrency;
    private string _currencyCode = string.Empty;
    private readonly RefreshVersionGate _refreshGate = new();

    [Inject] public required FinancialAlertsHttpClient HttpClient { get; set; }
    [Inject] public required ILogger<AlertsPage> Logger { get; set; }
    [Inject] public required ISnackbar Snackbar { get; set; }
    [Inject] public required ILoginService LoginService { get; set; }
    [Inject] public required ISnapshotRefreshCoordinator SnapshotRefreshCoordinator { get; set; }
    [Inject] public required ISnapshotService SnapshotService { get; set; }
    [Inject] public required CurrencyHttpClient CurrencyHttpClient { get; set; }
    [Inject] public required TransactionLogHttpClient TransactionLogHttpClient { get; set; }
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
        if (_isLoadingCurrencies) return;
        _isLoadingCurrencies = true;
        _currencyOptionsFailed = false;
        try
        {
            _currencies = [.. (await CurrencyHttpClient.GetAll()).OrderBy(currency => currency.ShortName)];
            _knownCurrencyIds = _currencies.Select(currency => currency.Id).ToHashSet();
            var preferred = await SettingsService.GetCurrencyAsync();
            _currencyCode = preferred.ShortName;
            _ownedCurrencyCodes = await LoadOwnedCurrencyCodesAsync();
            RebuildCurrencyOptions();
            // A new alert starts on the preferred currency; an alert being edited keeps its own, possibly empty, choice.
            if (_editingId is null && _form.CurrencyId is null)
                _form.CurrencyId = preferred.Id;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Unable to load currency options for the alert form");
            _currencyOptionsFailed = true;
        }
        finally
        {
            _isLoadingCurrencies = false;
        }

        await InvokeAsync(StateHasChanged);
    }

    // The currencies the user has transactions in; when that is unknown or empty the form falls back to the whole catalog.
    private async Task<IReadOnlyList<string>> LoadOwnedCurrencyCodesAsync()
    {
        try
        {
            var user = await LoginService.GetLoggedUser();
            return user is null ? [] : await TransactionLogHttpClient.GetCurrencies(user.UserId);
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Unable to load the currencies of the user's transactions; offering every currency");
            return [];
        }
    }

    /// <summary>
    /// Offers the currencies the user's transactions are in, plus the preferred one and the one the form currently holds,
    /// so an alert on a currency without transactions yet can still be edited. Falls back to the catalog when none are known.
    /// </summary>
    private void RebuildCurrencyOptions()
    {
        if (_ownedCurrencyCodes.Count == 0)
        {
            _currencyOptions = _currencies;
            return;
        }

        var owned = _ownedCurrencyCodes.ToHashSet(StringComparer.OrdinalIgnoreCase);
        _currencyOptions =
        [
            .. _currencies.Where(currency => owned.Contains(currency.ShortName)
                || currency.ShortName.Equals(_currencyCode, StringComparison.OrdinalIgnoreCase)
                || currency.Id == _form.CurrencyId)
        ];
    }

    private string? CurrencyCodeOf(FinancialAlertDto alert) =>
        alert.CurrencyId is int id ? _currencies.FirstOrDefault(currency => currency.Id == id)?.ShortName : null;

    /// <summary>
    /// The threshold is in the chosen currency for cash alerts (no code until one is chosen), otherwise in the preferred
    /// display currency.
    /// </summary>
    private string? ThresholdCurrencyCode => _form.RequiresCurrency
        ? _currencies.FirstOrDefault(currency => currency.Id == _form.CurrencyId)?.ShortName
        : _currencyCode;

    // Four fields share the row for cash alert types (the currency is one of them), three otherwise.
    private int FieldColumns => _form.RequiresCurrency ? 3 : 4;

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

    private static string SnapshotKey(int userId) => $"alerts-page:v2:{userId}";

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

        if (_form.RequiresCurrency && (_isLoadingCurrencies || _currencyOptionsFailed))
        {
            _errors.Add("Load currency options before saving this alert.");
            return;
        }

        if (!_form.HasValidScope)
        {
            _errors.Add("Select a currency for this alert.");
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

            Snackbar.Add(
                _editingId is null ? "Alert created." : _editingWasDisabled && _form.IsEnabled ? "Alert updated and turned on." : "Alert updated.",
                Severity.Success);
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
        _editingWasDisabled = !alert.IsEnabled;
        _editingNeedsCurrency = !alert.IsEnabled && AlertPresentation.NeedsCurrency(alert, _knownCurrencyIds);
        if (_editingNeedsCurrency)
        {
            // A stored currency that does not exist is dropped so the user has to pick a real one; saving then
            // turns the alert on unless they untick it.
            _form.CurrencyId = null;
            _form.IsEnabled = true;
        }
        RebuildCurrencyOptions();
    }

    private void CancelEdit()
    {
        if (_isSaving) return;
        ResetForm();
    }

    private void ResetForm()
    {
        _editingId = null;
        _editingWasDisabled = false;
        _editingNeedsCurrency = false;
        _form = new AlertFormModel { CurrencyId = SettingsService.GetCurrency().Id };
        RebuildCurrencyOptions();
        _formVersion++;
        _errors.Clear();
    }
}