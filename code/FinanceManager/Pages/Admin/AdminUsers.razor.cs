using MudBlazor;

namespace FinanceManager.WebUi.Pages.Admin;

public partial class AdminUsers
{
    string[] headings = { "User id", "User name", "Capacity", "Action" };

    List<(int, string, int)> rows = new List<(int, string, int)>()
    {
        (1, "Krishna", 55),
        (2, "Webb", 10),
        (3, "Nathanil", 20),
        (4, "Adara", 30),
        (5, "Cecilius", 40),
        (6, "Cicely", 90)
    };


    public MudTable<(int, string, int)> _table;
    private IEnumerable<(int, string, int)> _elements = new List<(int, string, int)>()
    {
        (1, "Krishna", 55),
        (2, "Webb", 10),
        (3, "Nathanil", 20),
        (4, "Adara", 30),
        (5, "Cecilius", 40),
        (6, "Cicely", 90)
    };

    protected override async Task OnInitializedAsync()
    {
        //_elements = await httpClient.GetFromJsonAsync<List<Element>>("webapi/periodictable");
    }

    private void PageChanged(int i)
    {
        _table.NavigateTo(i - 1);
    }

}