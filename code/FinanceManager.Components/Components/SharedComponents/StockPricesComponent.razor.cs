using FinanceManager.Components.HttpClients;
using FinanceManager.Domain.Entities.Currencies;
using FinanceManager.Domain.Entities.Stocks;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace FinanceManager.Components.Components.SharedComponents;

public partial class StockPricesComponent
{
    private List<StockPrice> _stockPrices = [];
    private bool _stockPriceExists = false;
    private readonly List<string> _currencies = ["USD", "PLN"];

    public decimal PricePerUnit { get; set; }
    public Currency? _existingCurrency { get; set; }
    public string? SelectedCurrency { get; set; }
    public DateTime? MissingDate { get; set; } = null;
    public DateRange DateRange { get; set; } = new(DateTime.UtcNow.AddMonths(-1), DateTime.UtcNow);

    [Parameter] public DateTime? Date { get; set; }
    [Parameter] public string Ticker { get; set; } = string.Empty;

    [Inject] private StockPriceHttpClient StockPriceHttpClient { get; set; } = default!;

    protected override async Task OnParametersSetAsync()
    {
        if (Date is not null)
        {
            var utcDate = Date.Value;
            if (utcDate.Kind != DateTimeKind.Utc)
                utcDate = utcDate.ToUniversalTime();
            if (string.IsNullOrEmpty(SelectedCurrency)) return;

            await StockPriceHttpClient.GetStockPrice(Ticker, 1, Date.Value).ContinueWith(task =>
            {
                if (task.IsCompletedSuccessfully)
                {
                    var stockPrice = task.Result;
                    if (stockPrice is not null)
                    {
                        _stockPriceExists = true;
                        PricePerUnit = stockPrice.PricePerUnit;
                    }
                    else
                    {
                        _stockPriceExists = false;
                        PricePerUnit = default;
                    }
                    StateHasChanged();
                }
            });
        }
        base.OnParametersSet();
    }

    private async Task<IEnumerable<string?>> Search(string value, CancellationToken token)
    {
        if (string.IsNullOrEmpty(value))
            return _currencies;

        return await Task.FromResult(_currencies.Where(x => x.Contains(value, StringComparison.InvariantCultureIgnoreCase)));
    }
    private void DateChanged((DateTime Start, DateTime End) dates)
    {
        DateRange = new DateRange(dates.Start, dates.End);

        if (!string.IsNullOrWhiteSpace(Ticker))
            _ = GetStockPriceAsync();
    }
    private async Task AddNewStock()
    {
        if (string.IsNullOrWhiteSpace(Ticker) || DateRange is null || DateRange.Start is null || DateRange.End is null || Date is null || PricePerUnit <= 0)
            return;

        if (string.IsNullOrWhiteSpace(SelectedCurrency))
            return;

        try
        {
            await StockPriceHttpClient.AddStockPrice(Ticker, PricePerUnit, 1, Date.Value);
            await GetStockPriceAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error adding stock price: {ex.Message}");
        }

        await UpdateLatestMissingDateAsync();
    }
    private async Task UpdateStockPrice()
    {

        if (string.IsNullOrWhiteSpace(Ticker) || DateRange is null || DateRange.Start is null || DateRange.End is null || Date is null || PricePerUnit <= 0)
            return;
        if (string.IsNullOrWhiteSpace(SelectedCurrency)) return;

        try
        {
            await StockPriceHttpClient.UpdateStockPrice(Ticker, PricePerUnit, 1, Date.Value);

            await GetStockPriceAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error adding stock price: {ex.Message}");
        }

        await UpdateLatestMissingDateAsync();
    }
    private async Task GetStockPriceAsync()
    {
        TimeSpan timeSpan = TimeSpan.FromDays(1);

        if (string.IsNullOrWhiteSpace(Ticker) || DateRange is null || DateRange.Start is null || DateRange.End is null)
            return;

        try
        {
            // TODO new endpoint should be added- Get and GetThisOrNextOlder !
            // GetStockPrices functions as GetThisOrNextOlder so finding missing stock does not work properly
            _stockPrices = [.. await StockPriceHttpClient.GetStockPrices(Ticker, DateRange.Start.Value, DateRange.End.Value, timeSpan)];
            var tickerCurrency = await StockPriceHttpClient.GetTickerCurrency(Ticker);

            if (tickerCurrency is not null)
            {
                _existingCurrency = tickerCurrency.Currency;
                SelectedCurrency = tickerCurrency.Currency.ShortName;
            }
            else
            {
                _existingCurrency = null;
                SelectedCurrency = DefaultCurrency.PLN.ShortName;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error fetching stock price: {ex.Message}");
        }

        await UpdateLatestMissingDateAsync();
        StateHasChanged();
    }
    private async Task UpdateLatestMissingDateAsync()
    {
        try
        {
            MissingDate = await StockPriceHttpClient.GetLatestMissingStockPrice(Ticker);
            Date = MissingDate;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"{ex}");
        }
    }

}