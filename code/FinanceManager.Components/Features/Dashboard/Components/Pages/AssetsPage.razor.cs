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
    // Cards whose reload for the selected range is in flight; their frames show the refresh indicator.
    private readonly HashSet<string> _refreshingCards = [];

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
        StateHasChanged();
    }

    // Stable callbacks let indicator-only renders leave unchanged card parameters alone.
    private Task SetHistoryRefreshing(bool refreshing) => SetCardRefreshing("assets-history", refreshing);
    private Task SetDistributionRefreshing(bool refreshing) => SetCardRefreshing("assets-distribution", refreshing);
    private Task SetPortfolioReturnRefreshing(bool refreshing) => SetCardRefreshing("portfolio-return", refreshing);
    private Task SetReturnAttributionRefreshing(bool refreshing) => SetCardRefreshing("return-attribution", refreshing);

    private Task SetCardRefreshing(string cardId, bool refreshing)
    {
        if (refreshing) _refreshingCards.Add(cardId);
        else _refreshingCards.Remove(cardId);
        StateHasChanged();
        return Task.CompletedTask;
    }
}