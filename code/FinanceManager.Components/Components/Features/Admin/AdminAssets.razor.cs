using FinanceManager.Components.Components.Shared.Dialogs;
using FinanceManager.Components.HttpClients;
using FinanceManager.Domain.Assets.Dtos;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace FinanceManager.Components.Components.Features.Admin;

public partial class AdminAssets : ComponentBase
{
    private bool _isLoading = true;
    private int _elementsPerPage = 20;
    private int _pagesCount;
    private string _searchText = string.Empty;

    private readonly List<string> _errors = [];
    private IReadOnlyList<AssetDto> _allElements = [];
    private IReadOnlyList<AssetDto> _filteredElements = [];
    private IEnumerable<AssetDto> _pagedElements = [];

    public MudTable<AssetDto>? Table { get; set; }
    public int SelectedPage { get; set; } = 1;

    [Inject] public required AssetHttpClient AssetHttpClient { get; set; }
    [Inject] public required IDialogService DialogService { get; set; }

    protected override async Task OnInitializedAsync()
    {
        try
        {
            _allElements = await AssetHttpClient.GetAssets();
        }
        catch (Exception)
        {
            _errors.Add("Failed while getting assets");
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

    private async Task DeleteAsset(AssetDto asset)
    {
        var parameters = new DialogParameters<ConfirmDeleteDialog>
        {
            { x => x.Title, "Delete asset" },
            { x => x.Message, $"Delete \"{asset.Name}\" and all its listings? This cannot be undone." }
        };
        var dialog = await DialogService.ShowAsync<ConfirmDeleteDialog>("Delete asset", parameters);
        var result = await dialog.Result;
        if (result is null || result.Canceled) return;

        if (!await AssetHttpClient.DeleteAsset(asset.Id))
        {
            _errors.Add($"Failed to delete asset {asset.Name}");
            return;
        }

        _allElements = _allElements.Where(item => item.Id != asset.Id).ToList();
        ApplyFilter();
    }

    private IEnumerable<AssetDto> PageItems(int page) =>
        _filteredElements.Skip((page - 1) * _elementsPerPage).Take(_elementsPerPage).ToList();

    private void ApplyFilter()
    {
        var query = _searchText.Trim();
        _filteredElements = string.IsNullOrWhiteSpace(query)
            ? _allElements
            : _allElements
                .Where(item =>
                    item.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                    (item.Isin?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false))
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
            SelectedPage = _pagesCount;

        _pagedElements = PageItems(SelectedPage);
    }
}