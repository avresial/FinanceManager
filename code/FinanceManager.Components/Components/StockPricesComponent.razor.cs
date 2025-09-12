using FinanceManager.Components.HttpContexts;
using FinanceManager.Domain.Entities;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace FinanceManager.Components.Components;
public partial class StockPricesComponent
{
    private DateRange _dateRange { get; set; } = new DateRange(DateTime.UtcNow.AddMonths(-1), DateTime.UtcNow);
    private decimal _pricePerUnit { get; set; }
    private string _existingCurrency { get; set; }
    private string _selectedCurrency { get; set; }
    private DateTime? _missingDate { get; set; } = null;

    private List<StockPrice> _stockPrices = [];
    private bool stockPriceExists = false;
    private DateTime? _Date;

    [Parameter]
    public DateTime? Date
    {
        get { return _Date; }
        set
        {
            _Date = value;

            if (value is not null)
            {

                var utcDate = value.Value;
                if (utcDate.Kind != DateTimeKind.Utc)
                    utcDate = utcDate.ToUniversalTime();

                StockPriceHttpContext.GetStockPrice(Ticker, _selectedCurrency, value.Value).ContinueWith(task =>
                {
                    if (task.IsCompletedSuccessfully)
                    {
                        var stockPrice = task.Result;
                        if (stockPrice is not null)
                        {
                            stockPriceExists = true;
                            _pricePerUnit = stockPrice.PricePerUnit;
                        }
                        else
                        {
                            stockPriceExists = false;
                            _pricePerUnit = default;
                        }
                        StateHasChanged();
                    }
                });
            }
        }
    }


    [Parameter] public string Ticker { get; set; } = string.Empty;

    [Inject] private StockPriceHttpContext StockPriceHttpContext { get; set; } = default!;

    private List<string> _currencies = ["USD", "PLN"];

    private async Task<IEnumerable<string>> Search(string value, CancellationToken token)
    {
        if (string.IsNullOrEmpty(value))
            return _currencies;

        return await Task.FromResult(_currencies.Where(x => x.Contains(value, StringComparison.InvariantCultureIgnoreCase)));
    }
    private void DateChanged((DateTime Start, DateTime End) dates)
    {
        _dateRange = new DateRange(dates.Start, dates.End);

        if (!string.IsNullOrWhiteSpace(Ticker))
            _ = GetStockPriceAsync();
    }
    private async Task AddNewStock()
    {
        if (string.IsNullOrWhiteSpace(Ticker) || _dateRange is null || _dateRange.Start is null || _dateRange.End is null || Date is null || _pricePerUnit <= 0)
            return;

        try
        {
            await StockPriceHttpContext.AddStockPrice(Ticker, _pricePerUnit, _selectedCurrency, Date.Value);
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

        if (string.IsNullOrWhiteSpace(Ticker) || _dateRange is null || _dateRange.Start is null || _dateRange.End is null || Date is null || _pricePerUnit <= 0)
            return;
        try
        {
            await StockPriceHttpContext.UpdateStockPrice(Ticker, _pricePerUnit, _selectedCurrency, Date.Value);

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

        if (string.IsNullOrWhiteSpace(Ticker) || _dateRange is null || _dateRange.Start is null || _dateRange.End is null)
            return;

        try
        {
            // TODO new endpoint should be added- Get and GetThisOrNextOlder !
            // GetStockPrices functions as GetThisOrNextOlder so finding missing stock does not work properly
            _stockPrices = [.. await StockPriceHttpContext.GetStockPrices(Ticker, _dateRange.Start.Value, _dateRange.End.Value, timeSpan)];
            var tickerCurrency = await StockPriceHttpContext.GetTickerCurrency(Ticker);

            if (tickerCurrency is not null)
            {
                _existingCurrency = tickerCurrency.Currency;
                _selectedCurrency = tickerCurrency.Currency;
            }
            else
            {
                _existingCurrency = null;
                _selectedCurrency = DefaultCurrency.Currency;
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
            _missingDate = await StockPriceHttpContext.GetLatestMissingStockPrice(Ticker);
            Date = _missingDate;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"{ex}");
        }
    }

}