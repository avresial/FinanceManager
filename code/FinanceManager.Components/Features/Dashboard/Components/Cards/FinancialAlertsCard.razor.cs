using FinanceManager.Application.Alerts.Models;
using FinanceManager.Components.Features.Alerts.HttpClients;
using FinanceManager.Domain.Alerts.Dtos;
using FinanceManager.Domain.Alerts.Enums;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;

namespace FinanceManager.Components.Features.Dashboard.Components.Cards;

public partial class FinancialAlertsCard : IDisposable
{
    private bool _isLoading = true;
    private bool _hasError;
    private bool _disposed;
    private bool _isLoadingDetail;
    private bool _hasDetailError;
    private List<FinancialAlertDto> _alerts = [];
    private List<AlertEvaluationOutcome> _triggered = [];
    private AlertEvaluationOutcome? _selectedAlert;
    private int _detailRequestVersion;

    [Parameter] public string Height { get; set; } = "300px";

    [Inject] public required FinancialAlertsHttpClient FinancialAlertsHttpClient { get; set; }
    [Inject] public required ILogger<FinancialAlertsCard> Logger { get; set; }

    protected override async Task OnInitializedAsync()
    {
        try
        {
            var outcomes = await FinancialAlertsHttpClient.EvaluateAsync();
            if (_disposed) return;

            _hasError = outcomes.Any(x => x.Status == AlertTriggerStatus.Error);
            _triggered = outcomes
                .Where(x => x.IsTriggered)
                .GroupBy(x => x.AlertId)
                .Select(group => group
                    .OrderByDescending(x => x.EvaluatedAt)
                    .ThenByDescending(x => x.TriggeredAt)
                    .First())
                .OrderByDescending(x => x.TriggeredAt)
                .ThenBy(x => x.AlertTitle)
                .ToList();
            _alerts = await FinancialAlertsHttpClient.GetAsync();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Unable to load dashboard financial alerts");
            _hasError = true;
        }
        finally
        {
            if (!_disposed)
                _isLoading = false;
        }
    }

    private async Task SelectAlertAsync(AlertEvaluationOutcome alert)
    {
        var requestVersion = Interlocked.Increment(ref _detailRequestVersion);
        _selectedAlert = alert;
        _hasDetailError = false;
        _isLoadingDetail = true;
        StateHasChanged();

        try
        {
            var detailedOutcome = await FinancialAlertsHttpClient.EvaluateAsync(
                alert.AlertId,
                includeAllMatchingTransactions: true);

            if (_disposed || requestVersion != _detailRequestVersion)
                return;

            if (detailedOutcome is null)
            {
                _hasDetailError = true;
                return;
            }

            _selectedAlert = detailedOutcome;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Unable to load details for financial alert {AlertId}", alert.AlertId);
            if (!_disposed && requestVersion == _detailRequestVersion)
                _hasDetailError = true;
        }
        finally
        {
            if (!_disposed && requestVersion == _detailRequestVersion)
            {
                _isLoadingDetail = false;
                StateHasChanged();
            }
        }
    }

    private void Back()
    {
        Interlocked.Increment(ref _detailRequestVersion);
        _selectedAlert = null;
        _isLoadingDetail = false;
        _hasDetailError = false;
    }

    public void Dispose()
    {
        _disposed = true;
        Interlocked.Increment(ref _detailRequestVersion);
    }
}