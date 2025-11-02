using FinanceManager.Components.HttpContexts;
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

    private readonly List<string> _errors = [];
    private IEnumerable<UserDetails> _elements = [];

    [Inject] required public AdministrationUsersHttpContext AdministrationUsersHttpContext { get; set; }
    [Inject] required public IUserService UserService { get; set; }

    public int SelectedPage { get; set; } = 1;
    public MudTable<UserDetails>? Table { get; set; }

    protected override async Task OnInitializedAsync()
    {
        _usersCount = await AdministrationUsersHttpContext.GetUsersCount();
        _elements = await AdministrationUsersHttpContext.GetUsers(0, _recordsPerPage);
        _pagesCount = (int)Math.Ceiling((double)_usersCount / _recordsPerPage);
    }

    private async Task PageChanged(int i) =>
        _elements = await AdministrationUsersHttpContext.GetUsers((i - 1) * _recordsPerPage, _recordsPerPage);


    private async Task RemoveUser(int userId)
    {
        var result = await UserService.Delete(userId);
        if (!result)
        {
            _errors.Add($"Failed to delete user {userId}");
            return;
        }

        _usersCount = await AdministrationUsersHttpContext.GetUsersCount();

        _elements = await AdministrationUsersHttpContext.GetUsers((SelectedPage - 1) * _recordsPerPage, _recordsPerPage);
    }
}