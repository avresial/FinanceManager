using FinanceManager.Components.Features.Dashboard.Services;
using FinanceManager.Components.Features.Identity.Services;
using FinanceManager.Components.Features.Insights.HttpClients;
using FinanceManager.Components.Shared.Services;
using FinanceManager.Domain.Identity.Entities;
using FinanceManager.Domain.Identity.Services;
using FinanceManager.Domain.Insights.Entities;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace FinanceManager.Components.Features.Dashboard.Components.Cards;

public partial class FinancialInsightsCarousel : IDisposable
{
    private bool _isLoading;
    private bool _disposed;
    private List<FinancialInsight> _insights = [];
    private readonly RefreshVersionGate _refreshGate = new();

    [Inject] public required FinancialInsightsHttpClient FinancialInsightsHttpClient { get; set; }
    [Inject] public required ILoginService LoginService { get; set; }
    [Inject] public required DashboardCardsSnapshotStore SnapshotStore { get; set; }
    [Inject] public required ISnackbar Snackbar { get; set; }

    [Parameter] public string Height { get; set; } = "300px";
    [Parameter] public int Count { get; set; } = 3;
    [Parameter] public int? AccountId { get; set; }

    protected override async Task OnParametersSetAsync() => await LoadInsightsAsync();

    private async Task LoadInsightsAsync()
    {
        var version = _refreshGate.Claim();
        var user = await LoginService.GetLoggedUser();
        if (_disposed || !_refreshGate.IsCurrent(version)) return;
        if (user is null)
        {
            _insights = [];
            _isLoading = false;
            StateHasChanged();
            return;
        }

        var result = await SnapshotStore.RefreshInsightsAsync(
            user.UserId,
            Count,
            AccountId,
            _refreshGate,
            version,
            async () => await FinancialInsightsHttpClient.GetLatestAsync(Count, AccountId),
            onSnapshotPainted: ShowInsights,
            onSnapshotMissing: ShowLoading,
            onRefreshed: ShowInsights);
        if (_disposed || !_refreshGate.IsCurrent(version)) return;

        _isLoading = false;
        if (result.IsBlockingFailure)
        {
            Snackbar.Add("Unable to load financial insights.", Severity.Error);
        }
        // Outcomes that never reach ShowInsights (blocking failure, empty response) still have to
        // clear the spinner painted by ShowLoading.
        StateHasChanged();
    }

    private Task ShowInsights(List<FinancialInsight> insights)
    {
        if (_disposed) return Task.CompletedTask;

        _insights = insights;
        _isLoading = false;
        return InvokeAsync(StateHasChanged);
    }

    private Task ShowLoading()
    {
        if (_disposed) return Task.CompletedTask;

        _isLoading = true;
        return InvokeAsync(StateHasChanged);
    }

    public void Dispose() => _disposed = true;
}