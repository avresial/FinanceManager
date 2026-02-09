using FinanceManager.Components.Components.SharedComponents;
using FinanceManager.Components.HttpClients;
using FinanceManager.Domain.Entities.Bonds;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace FinanceManager.Components.Components.Admin;

public partial class AdminBondDetails : ComponentBase
{
    private bool _isLoading = true;
    private int _elementsPerPage = 20;
    private int _pagesCount;
    private string _searchText = string.Empty;

    private readonly List<string> _errors = [];
    private IReadOnlyList<BondDetails> _allElements = [];
    private IReadOnlyList<BondDetails> _filteredElements = [];
    private IEnumerable<BondDetails> _pagedElements = [];

    public MudTable<BondDetails>? Table { get; set; }
    public int SelectedPage { get; set; } = 1;

    [Inject] public required BondDetailsHttpClient BondDetailsHttpClient { get; set; }
    [Inject] public required IDialogService DialogService { get; set; }

    protected override async Task OnInitializedAsync()
    {
        try
        {
            _allElements = await BondDetailsHttpClient.GetAll();
        }
        catch (Exception)
        {
            _errors.Add("Failed while getting bond details");
            _isLoading = false;
            return;
        }

        ApplyFilter();

        _isLoading = false;
    }

    private void PageChanged(int i)
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

    private async Task ShowInfo(BondDetails bond)
    {
        var parameters = new DialogParameters
        {
            [nameof(BondDetailsInfoDialog.Details)] = bond,
        };
        var options = new DialogOptions { CloseOnEscapeKey = true, MaxWidth = MaxWidth.Small, FullWidth = true };
        await DialogService.ShowAsync<BondDetailsInfoDialog>("Bond details", parameters, options);
    }

    private async Task DeleteBondDetails(BondDetails bond)
    {
        var options = new DialogOptions { CloseOnEscapeKey = true };
        var dialog = await DialogService.ShowAsync<ConfirmDeleteBondDetailsDialog>("Delete bond details", options);
        var result = await dialog.Result;

        if (result is null || result.Canceled)
        {
            return;
        }

        var removed = await BondDetailsHttpClient.Delete(bond.Id);
        if (!removed)
        {
            _errors.Add($"Failed to delete bond details {bond.Id}");
            return;
        }

        _allElements = _allElements.Where(item => item.Id != bond.Id).ToList();
        ApplyFilter();
    }

    private IEnumerable<BondDetails> PageItems(int page) =>
        _filteredElements.Skip((page - 1) * _elementsPerPage).Take(_elementsPerPage).ToList();

    private void ApplyFilter()
    {
        _filteredElements = string.IsNullOrWhiteSpace(_searchText)
            ? _allElements
            : _allElements
                .Where(item => item.Name.Contains(_searchText, StringComparison.OrdinalIgnoreCase))
                .ToList();

        _pagesCount = _filteredElements.Count == 0
            ? 0
            : (int)Math.Ceiling((double)_filteredElements.Count / _elementsPerPage);

        if (_pagesCount == 0)
        {
            SelectedPage = 1;
            _pagedElements = [];
            return;
        }

        if (SelectedPage > _pagesCount)
        {
            SelectedPage = _pagesCount;
        }

        _pagedElements = PageItems(SelectedPage);
    }
}
