using FinanceManager.Components.HttpClients;
using FinanceManager.Domain.Entities.Users;
using FinanceManager.Domain.Services;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace FinanceManager.Components.Components.Admin;

public partial class AdminUsers : ComponentBase
{
    private int _usersCount = 0;
    private int _recordsPerPage = 20;
    private int _pagesCount;
    private string _searchText = string.Empty;

    private readonly List<string> _errors = [];
    private IReadOnlyList<UserDetails> _allElements = [];
    private IReadOnlyList<UserDetails> _filteredElements = [];
    private IEnumerable<UserDetails> _pagedElements = [];

    [Inject] required public AdministrationUsersHttpClient AdministrationUsersHttpClient { get; set; }
    [Inject] required public IUserService UserService { get; set; }

    public int SelectedPage { get; set; } = 1;
    public MudTable<UserDetails>? Table { get; set; }

    protected override async Task OnInitializedAsync()
    {
        _usersCount = await AdministrationUsersHttpClient.GetUsersCount();
        if (_usersCount > 0)
        {
            _allElements = await AdministrationUsersHttpClient.GetUsers(0, _usersCount);
            ApplyFilter();
        }
    }

    private async Task PageChanged(int i)
    {
        SelectedPage = i;
        _pagedElements = PageItems(i);
    }

    private void OnSearchChanged(string value)
    {
        _searchText = value ?? string.Empty;
        SelectedPage = 1;
        ApplyFilter();
    }

    private IEnumerable<UserDetails> PageItems(int page) =>
        _filteredElements.Skip((page - 1) * _recordsPerPage).Take(_recordsPerPage).ToList();

    private void ApplyFilter()
    {
        _filteredElements = string.IsNullOrWhiteSpace(_searchText)
            ? _allElements
            : _allElements.Where(item => item.Login.Contains(_searchText, StringComparison.OrdinalIgnoreCase) || item.UserId.ToString().Contains(_searchText) || item.PricingLevel.ToString().Contains(_searchText)).ToList();

        _pagesCount = _filteredElements.Count == 0 ? 0 : (int)Math.Ceiling((double)_filteredElements.Count / _recordsPerPage);

        if (_pagesCount == 0)
        {
            SelectedPage = 1;
            _pagedElements = [];
            return;
        }

        if (SelectedPage > _pagesCount)
            SelectedPage = _pagesCount;

        _pagedElements = PageItems(SelectedPage);
    }


    private async Task RemoveUser(int userId)
    {
        var result = await UserService.Delete(userId);
        if (!result)
        {
            _errors.Add($"Failed to delete user {userId}");
            return;
        }

        _usersCount = await AdministrationUsersHttpClient.GetUsersCount();

        _allElements = await AdministrationUsersHttpClient.GetUsers(0, _usersCount);
        ApplyFilter();
    }
}