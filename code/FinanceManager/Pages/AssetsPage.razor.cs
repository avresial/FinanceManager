namespace FinanceManager.WebUi.Pages;

public partial class AssetsPage
{
    private const int _unitHeight = 190;

    public DateTime StartDateTime { get; set; }
    public DateTime EndDate { get; set; } = DateTime.UtcNow;
    public void DateChanged((DateTime Start, DateTime End) changed)
    {
        StartDateTime = changed.Start;
        EndDate = changed.End;
        StateHasChanged();
    }
}