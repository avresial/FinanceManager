using FinanceManager.Components.Components.SharedComponents;
using FinanceManager.Components.HttpClients;
using FinanceManager.Domain.Entities.Stocks;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace FinanceManager.Components.Components.Admin;

public partial class AdminStocks : ComponentBase
{
    private bool _isLoading = true;
    private int _elementsPerPage = 20;
    private int _pagesCount;
    private string _searchText = string.Empty;

    private readonly List<string> _errors = [];
    private IReadOnlyList<StockDetails> _allElements = [];
    private IReadOnlyList<StockDetails> _filteredElements = [];
    private IEnumerable<StockDetails> _pagedElements = [];

    public MudTable<StockDetails>? Table { get; set; }
    public int SelectedPage { get; set; } = 1;

    [Inject] public required StockPriceHttpClient StockPriceHttpClient { get; set; }
    [Inject] public required IDialogService DialogService { get; set; }

    protected override async Task OnInitializedAsync()
    {
        try
        {
            _allElements = await StockPriceHttpClient.GetStocks();
        }
        catch (Exception)
        {
            _errors.Add("Failed while getting stocks");
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

    private async Task DeleteStock(StockDetails ticker)
    {
        var options = new DialogOptions { CloseOnEscapeKey = true };
        var dialog = await DialogService.ShowAsync<ConfirmDeleteStockDialog>("Delete stock", options);
        var result = await dialog.Result;

        if (result is null || result.Canceled)
        {
            return;
        }

        var removed = await StockPriceHttpClient.DeleteStock(ticker.Ticker);
        if (!removed)
        {
            _errors.Add($"Failed to delete stock {ticker.Ticker}");
            return;
        }

        _allElements = _allElements.Where(item => item.Ticker != ticker.Ticker).ToList();
        ApplyFilter();
    }

    private IEnumerable<StockDetails> PageItems(int page) =>
        _filteredElements.Skip((page - 1) * _elementsPerPage).Take(_elementsPerPage).ToList();

    private void ApplyFilter()
    {
        var query = _searchText.Trim();
        _filteredElements = string.IsNullOrWhiteSpace(query)
            ? _allElements
            : _allElements
                .Where(item =>
                    item.Ticker.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                    item.Name.Contains(query, StringComparison.OrdinalIgnoreCase))
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
