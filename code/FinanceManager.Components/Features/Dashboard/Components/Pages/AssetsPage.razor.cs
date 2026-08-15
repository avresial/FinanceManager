using FinanceManager.Components.Shared.Helpers;
using Microsoft.AspNetCore.Components;

namespace FinanceManager.Components.Features.Dashboard.Components.Pages;

public partial class AssetsPage : ComponentBase
{
    private const int _unitHeight = 190;

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
}