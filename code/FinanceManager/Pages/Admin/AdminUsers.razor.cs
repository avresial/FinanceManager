using FinanceManager.Components.Services;
using FinanceManager.Domain.Entities.User;
using FinanceManager.Domain.Services;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace FinanceManager.WebUi.Pages.Admin;

public partial class AdminUsers
{
    private int _usersCount = 0;
    private int _recordsPerPage = 20;
    private int _pagesCount;

    List<string> _errors = [];

    [Inject] required public AdministrationUsersService AdministrationUsersService { get; set; }
    [Inject] required public IUserService UserService { get; set; }


    public MudTable<UserDetails>? _table;
    private IEnumerable<UserDetails> _elements = [];

    protected override async Task OnInitializedAsync()
    {
        _usersCount = await AdministrationUsersService.GetUsersCount();
        _elements = await AdministrationUsersService.GetUsers();
        _pagesCount = (int)Math.Ceiling((double)_usersCount / _recordsPerPage);
    }

    private void PageChanged(int i)
    {
        _table?.NavigateTo(i - 1);
    }

    private async Task RemoveUser(int userId)
    {
        var result = await UserService.Delete(userId);
        if (!result)
        {
            _errors.Add($"Failed to delete user {userId}");
            return;
        }

        _usersCount = await AdministrationUsersService.GetUsersCount();
        _elements = await AdministrationUsersService.GetUsers();
    }

}