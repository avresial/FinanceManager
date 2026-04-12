using FinanceManager.Components.HttpClients;
using FinanceManager.Components.Services;
using FinanceManager.Domain.Entities.Stocks;
using FinanceManager.Domain.Services;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace FinanceManager.Components.Components.SharedComponents;

public partial class StockPricesComponent
{
    private List<StockPrice> _stockPrices = [];
    private List<StockDetails> _allStocks = [];
    private StockDetails? _selectedStock;

    public DateRange DateRange { get; set; } = new(DateTime.UtcNow.AddMonths(-1), DateTime.UtcNow);

    [Parameter] public string Ticker { get; set; } = string.Empty;

    [Inject] private StockPriceHttpClient StockPriceHttpClient { get; set; } = default!;
    [Inject] private ISettingsService SettingsService { get; set; } = default!;

    protected override async Task OnInitializedAsync()
    {
        try
        {
            _allStocks = await StockPriceHttpClient.GetStocks();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading stocks: {ex.Message}");
        }
    }

    private async Task<IEnumerable<StockDetails>> SearchStocks(string value, CancellationToken token)
    {
        if (string.IsNullOrWhiteSpace(value))
            return _allStocks.Take(50);

        var searchValue = value.ToUpperInvariant();
        return await Task.FromResult(_allStocks
            .Where(x => x.Ticker.Contains(searchValue, StringComparison.OrdinalIgnoreCase) ||
                       x.Name.Contains(searchValue, StringComparison.OrdinalIgnoreCase))
            .Take(50)
            .ToList());
    }

    private async Task OnStockSelected(StockDetails? stock)
    {
        _selectedStock = stock;

        if (stock is null)
        {
            Ticker = string.Empty;
            _stockPrices = [];
            StateHasChanged();
            return;
        }

        Ticker = stock.Ticker;
        await GetStockPriceAsync();
    }

    private void DateChanged((DateTime Start, DateTime End) dates)
    {
        DateRange = new DateRange(dates.Start, dates.End);

        if (!string.IsNullOrWhiteSpace(Ticker))
            _ = GetStockPriceAsync();
    }

    private async Task GetStockPriceAsync()
    {
        TimeSpan timeSpan = TimeSpan.FromDays(1);

        if (string.IsNullOrWhiteSpace(Ticker) || DateRange is null || DateRange.Start is null || DateRange.End is null)
            return;

        try
        {
            var currency = SettingsService.GetCurrency();
            _stockPrices = [.. await StockPriceHttpClient.GetStockPrices(Ticker, currency.Id, DateRange.Start.Value, DateRange.End.Value, timeSpan)];
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error fetching stock price: {ex.Message}");
        }

        StateHasChanged();
    }
}