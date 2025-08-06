using FinanceManager.Components.Services;
using FinanceManager.Domain.Entities.User;
using FinanceManager.Domain.Services;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace FinanceManager.WebUi.Pages.Admin;

public partial class AdminUsers
{
    private int _selectedPage;
    private int _usersCount = 0;
    private int _recordsPerPage = 20;
    private int _pagesCount;

    private List<string> _errors = [];
    private IEnumerable<UserDetails> _elements = [];

    [Inject] required public AdministrationUsersService AdministrationUsersService { get; set; }
    [Inject] required public IUserService UserService { get; set; }

    public MudTable<UserDetails>? _table;

    protected override async Task OnInitializedAsync()
    {
        var usersCount = await AdministrationUsersService.GetUsersCount();
        _usersCount = usersCount is null ? 0 : usersCount.Value;

        _elements = await AdministrationUsersService.GetUsers(0, _recordsPerPage);
        _pagesCount = (int)Math.Ceiling((double)_usersCount / _recordsPerPage);
    }

    private async Task PageChanged(int i)
    {
        _elements = await AdministrationUsersService.GetUsers((i - 1) * _recordsPerPage, _recordsPerPage);
    }

    private async Task RemoveUser(int userId)
    {
        var result = await UserService.Delete(userId);
        if (!result)
        {
            _errors.Add($"Failed to delete user {userId}");
            return;
        }

        var usersCount = await AdministrationUsersService.GetUsersCount();
        _usersCount = usersCount is null ? 0 : usersCount.Value;

        _elements = await AdministrationUsersService.GetUsers((_selectedPage - 1) * _recordsPerPage, _recordsPerPage);
    }

}