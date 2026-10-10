using FinanceManager.Components.Features.Dashboard.Models;
using FinanceManager.Components.Shared.Helpers;
using Microsoft.AspNetCore.Components;

namespace FinanceManager.Components.Features.Dashboard.Components.Pages;

public partial class LiabilitiesPage : ComponentBase
{
    private static readonly DashboardGridCard[] _cards =
    [
        new("liabilities-history", "Liabilities value over time", new(12, 12)),
        new("liabilities-distribution", "Liabilities distribution", new(4, 6)),
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

    private Task SetCardRefreshing(string cardId, bool refreshing)
    {
        if (refreshing) _refreshingCards.Add(cardId);
        else _refreshingCards.Remove(cardId);
        StateHasChanged();
        return Task.CompletedTask;
    }
}