using FinanceManager.Domain.Entities.Accounts.Entries;
using MudBlazor;

namespace FinanceManager.WebUi.Pages.Admin;
public partial class AdminLabelPage
{
    private int _selectedPage;
    private int _labelsCount = 0;
    private int _recordsPerPage = 20;
    private int _pagesCount;

    private List<string> _errors = [];
    private IEnumerable<FinancialLabel> _elements = [];

    public MudTable<FinancialLabel>? _table;

    protected override async Task OnInitializedAsync()
    {
        _labelsCount = 1;

        _elements = [new() { Name = $"Test label {Random.Shared.Next(0, 100)}" }];
        _pagesCount = (int)Math.Ceiling((double)_labelsCount / _recordsPerPage);
    }

    private async Task PageChanged(int i)
    {
        _elements = [new() { Name = $"Test label {Random.Shared.Next(0, 100)}" }];
    }

    private async Task RemoveLabel(int labelId)
    {
        var result = false;
        //var result = await UserService.Delete(userId);
        if (!result)
        {
            _errors.Add($"Failed to delete label {labelId}");
            return;
        }

        //_labelsCount = usersCount is null ? 0 : usersCount.Value;
        _elements = [new() { Name = $"Test label {Random.Shared.Next(0, 100)}" }];
        //_elements = await AdministrationUsersService.GetUsers((_selectedPage - 1) * _recordsPerPage, _recordsPerPage);
    }
}