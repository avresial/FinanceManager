using FinanceManager.Application.Alerts.Models;
using FinanceManager.Components.Features.Alerts.HttpClients;
using FinanceManager.Components.Features.Dashboard.Models;
using FinanceManager.Components.Shared.Services;
using FinanceManager.Domain.Alerts.Enums;
using FinanceManager.Domain.Identity.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;

namespace FinanceManager.Components.Features.Dashboard.Components.Cards;

public partial class FinancialAlertsCard : IDisposable
{
    private readonly RefreshVersionGate _gate = new();
    private bool _isLoading = true;
    private bool _hasError;
    private bool _disposed;
    private bool _isLoadingDetail;
    private bool _hasDetailError;
    private int _configuredCount;
    private FinancialAlertsSummaryModel? _summary;
    private List<AlertEvaluationOutcome> _triggered = [];
    private AlertEvaluationOutcome? _selectedAlert;
    private int _detailRequestVersion;

    [Parameter] public string Height { get; set; } = "300px";

    [Inject] public required FinancialAlertsHttpClient FinancialAlertsHttpClient { get; set; }
    [Inject] public required ILoginService LoginService { get; set; }
    [Inject] public required ISnapshotRefreshCoordinator Coordinator { get; set; }
    [Inject] public required ILogger<FinancialAlertsCard> Logger { get; set; }

    protected override async Task OnInitializedAsync()
    {
        var version = _gate.Claim();
        try
        {
            var user = await LoginService.GetLoggedUser();
            if (!_gate.IsCurrent(version)) return;
            if (user is null)
            {
                Show(new FinancialAlertsSummaryModel(0, [], false));
                _isLoading = false;
                return;
            }

            var result = await Coordinator.RunAsync(new SnapshotRefreshRequest<FinancialAlertsSnapshot, FinancialAlertsSummaryModel>
            {
                Key = $"financial-alerts:{user.UserId}",
                Gate = _gate,
                ClaimedVersion = version,
                ToModel = snapshot => snapshot.UserId == user.UserId ? snapshot.Summary : null,
                FetchAsync = async () =>
                {
                    var outcomesTask = FinancialAlertsHttpClient.EvaluateAsync();
                    var alertsTask = FinancialAlertsHttpClient.GetAsync();
                    await Task.WhenAll(outcomesTask, alertsTask);
                    return BuildSummary(outcomesTask.Result, alertsTask.Result.Count);
                },
                ToSnapshot = summary => new FinancialAlertsSnapshot { UserId = user.UserId, Summary = summary },
                OnSnapshotPainted = ShowAndRefresh,
                OnSnapshotMissing = () =>
                {
                    _isLoading = true;
                    StateHasChanged();
                    return Task.CompletedTask;
                },
                OnRefreshed = ShowAndRefresh,
            });
            if (!_gate.IsCurrent(version)) return;
            _hasError = result.IsBlockingFailure || (_summary?.HasEvaluationError ?? false);
            _isLoading = false;
        }
        catch (Exception ex)
        {
            if (!_gate.IsCurrent(version)) return;
            Logger.LogError(ex, "Unable to load dashboard financial alerts");
            _hasError = true;
            _isLoading = false;
        }
    }

    private static FinancialAlertsSummaryModel BuildSummary(List<AlertEvaluationOutcome> outcomes, int configuredCount) =>
        new(
            configuredCount,
            [.. outcomes
                .Where(x => x.IsTriggered)
                .GroupBy(x => x.AlertId)
                .Select(group => group
                    .OrderByDescending(x => x.EvaluatedAt)
                    .ThenByDescending(x => x.TriggeredAt)
                    .First())
                .OrderByDescending(x => x.TriggeredAt)
                .ThenBy(x => x.AlertTitle)
                .Select(FinancialAlertSummaryItem.From)],
            outcomes.Any(x => x.Status == AlertTriggerStatus.Error));

    private void Show(FinancialAlertsSummaryModel summary)
    {
        _summary = summary;
        _configuredCount = summary.ConfiguredCount;
        _triggered = [.. summary.Triggered.Select(x => x.ToOutcome())];
        _hasError = summary.HasEvaluationError;
    }

    private Task ShowAndRefresh(FinancialAlertsSummaryModel summary)
    {
        Show(summary);
        _isLoading = false;
        StateHasChanged();
        return Task.CompletedTask;
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
        _gate.Claim();
        Interlocked.Increment(ref _detailRequestVersion);
    }
}