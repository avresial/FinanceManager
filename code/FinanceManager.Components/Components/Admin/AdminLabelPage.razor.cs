using FinanceManager.Components.HttpClients;
using FinanceManager.Domain.Entities.Shared.Accounts;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace FinanceManager.Components.Components.Admin;

public partial class AdminLabelPage : ComponentBase
{
    private bool _isLoading = true;
    private int _labelsCount;
    private int _elementsPerPage = 20;
    private int _pagesCount;
    private string _searchText = string.Empty;

    private List<string> _errors = [];
    private IReadOnlyList<FinancialLabel> _allElements = [];
    private IReadOnlyList<FinancialLabel> _filteredElements = [];
    private IEnumerable<FinancialLabel> _elements = [];

    public MudTable<FinancialLabel>? Table { get; set; }
    public int SelectedPage { get; set; } = 1;

    [Inject] public required FinancialLabelHttpClient FinancialLabelHttpClient { get; set; }
    [Inject] public required NavigationManager NavigationManager { get; set; }

    protected override async Task OnInitializedAsync()
    {
        try
        {
            _labelsCount = await FinancialLabelHttpClient.GetCount();
            if (_labelsCount == 0)
            {
                NavigationManager.NavigateTo("Admin/AddLabel");
                return;
            }
        }
        catch (Exception)
        {
            _errors.Add("Failed while getting labels count");
            _isLoading = false;
            return;
        }

        try
        {
            _allElements = (await FinancialLabelHttpClient.Get(0, _labelsCount)).ToList();
            ApplyFilter();
        }
        catch (Exception)
        {
            _errors.Add("Failed while getting labels");
            _isLoading = false;
            return;
        }

        _isLoading = false;
    }

    private async Task PageChanged(int i)
    {
        try
        {
            SelectedPage = i;
            _elements = PageItems(i);
        }
        catch (Exception)
        {
            _errors.Add("Failed while getting labels");
            return;
        }
    }

    private async Task RemoveLabel(int labelId)
    {
        var result = await FinancialLabelHttpClient.Delete(labelId);
        if (!result)
        {
            _errors.Add($"Failed to delete label {labelId}");
            return;
        }

        _elements = PageItems(SelectedPage);
    }

    private void OnSearchChanged(string value)
    {
        _searchText = value ?? string.Empty;
        SelectedPage = 1;
        ApplyFilter();
    }

    private IEnumerable<FinancialLabel> PageItems(int page) =>
        _filteredElements.Skip((page - 1) * _elementsPerPage).Take(_elementsPerPage).ToList();

    private void ApplyFilter()
    {
        _filteredElements = string.IsNullOrWhiteSpace(_searchText)
            ? _allElements
            : _allElements.Where(item => item.Name.Contains(_searchText, StringComparison.OrdinalIgnoreCase)).ToList();

        _pagesCount = _filteredElements.Count == 0 ? 0 : (int)Math.Ceiling((double)_filteredElements.Count / _elementsPerPage);

        if (_pagesCount == 0)
        {
            SelectedPage = 1;
            _elements = [];
            return;
        }

        if (SelectedPage > _pagesCount)
            SelectedPage = _pagesCount;

        _elements = PageItems(SelectedPage);
    }
}