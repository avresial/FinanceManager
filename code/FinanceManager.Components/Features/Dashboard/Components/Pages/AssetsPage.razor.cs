using FinanceManager.Components.Features.Dashboard.Models;
using FinanceManager.Components.Shared.Helpers;
using Microsoft.AspNetCore.Components;

namespace FinanceManager.Components.Features.Dashboard.Components.Pages;

public partial class AssetsPage : ComponentBase
{
    private static readonly DashboardGridCard[] _cards =
    [
        new("assets-history", "Assets value over time", new(12, 12)),
        new("assets-distribution", "Assets distribution", new(4, 6)),
        new("portfolio-return", "Portfolio return", new(6, 12)),
        new("return-attribution", "Return attribution", new(4, 6)),
        new("investment-paycheck", "Investment paycheck", new(4, 6)),
        new("investment-rate", "Investment rate", new(4, 6)),
        new("diversification", "Diversification", new(6, 6)),
        new("fee-drag", "ETF fee drag", new(6, 6)),
    ];
    // Cards that reload when the selected range changes and report it through RefreshingChanged.
    private static readonly string[] _rangeDependentCards = ["assets-history", "assets-distribution", "portfolio-return", "return-attribution"];
    // Cards whose reload for the selected range is in flight; their frames show the refresh indicator.
    private readonly HashSet<string> _refreshingCards = [];
    // Cards that still owe a reload for a range the user picked. Only those reloads show the indicator:
    // the first load and background revalidation of a painted snapshot stay silent, and a cache miss
    // shows the card's own loading state instead. #985
    private readonly HashSet<string> _pendingRangeChangeCards = [];

    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; } = DateTime.UtcNow;

    protected override void OnInitialized()
    {
        // Same default range as the Dashboard, from the one shared entry point. #700
        var (Start, End) = DateRangeHelper.GetDefaultOverviewRange(DateTime.UtcNow);

        StartDate = Start;
        EndDate = End;

        base.OnInitialized();
    }

    public void DateChanged((DateTime Start, DateTime End) changed)
    {
        StartDate = changed.Start;
        EndDate = changed.End;
        _pendingRangeChangeCards.UnionWith(_rangeDependentCards);
        StateHasChanged();
    }

    // Stable callbacks let indicator-only renders leave unchanged card parameters alone.
    private Task SetHistoryRefreshing(bool refreshing) => SetCardRefreshing("assets-history", refreshing);
    private Task SetDistributionRefreshing(bool refreshing) => SetCardRefreshing("assets-distribution", refreshing);
    private Task SetPortfolioReturnRefreshing(bool refreshing) => SetCardRefreshing("portfolio-return", refreshing);
    private Task SetReturnAttributionRefreshing(bool refreshing) => SetCardRefreshing("return-attribution", refreshing);

    private Task SetCardRefreshing(string cardId, bool refreshing)
    {
        bool indicatorChanged;
        if (refreshing)
        {
            indicatorChanged = _pendingRangeChangeCards.Contains(cardId) && _refreshingCards.Add(cardId);
        }
        else
        {
            // Cards report only their current reload finishing, so this settles the user's range change.
            _pendingRangeChangeCards.Remove(cardId);
            indicatorChanged = _refreshingCards.Remove(cardId);
        }

        if (indicatorChanged) StateHasChanged();
        return Task.CompletedTask;
    }
}