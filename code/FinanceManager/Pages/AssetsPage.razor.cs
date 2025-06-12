using FinanceManager.Components.Helpers;

namespace FinanceManager.WebUi.Pages;

public partial class AssetsPage
{
    private const int _unitHeight = 190;

    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; } = DateTime.UtcNow;

    protected override void OnInitialized()
    {
        var (Start, End) = DateRangeHelper.GetCurrentMonthRange();

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