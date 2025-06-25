using FinanceManager.Components.HttpContexts;
using FinanceManager.Domain.Entities;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace FinanceManager.Components.Components;
public partial class StockPricesComponent
{
    private DateRange _dateRange { get; set; }
    private decimal _pricePerUnit { get; set; }
    private string _currency { get; set; } = "pln";
    private DateTime? _date { get; set; }

    private List<StockPrice> _stockPrices = [];
    private List<DateTime> _missingDates = [];
    [Parameter] public string Ticker { get; set; } = string.Empty;

    [Inject] private StockPriceHttpContext StockPriceHttpContext { get; set; } = default!;

    private void DateChanged((DateTime Start, DateTime End) dates)
    {
        _dateRange = new DateRange(dates.Start, dates.End);
    }
    private async Task AddNewStock()
    {
        if (string.IsNullOrWhiteSpace(Ticker) || _dateRange is null || _dateRange.Start is null || _dateRange.End is null || _date is null || _pricePerUnit <= 0)
            return;
        try
        {
            var stockPrice = await StockPriceHttpContext.AddStockPrice(Ticker, _pricePerUnit, _currency, _date.Value);
            if (stockPrice != null)
            {
                _stockPrices.Add(stockPrice);
                await GetStockPriceAsync();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error adding stock price: {ex.Message}");
        }

    }
    private async Task GetStockPriceAsync()
    {
        TimeSpan timeSpan = TimeSpan.FromDays(1);


        if (string.IsNullOrWhiteSpace(Ticker) || _dateRange is null || _dateRange.Start is null || _dateRange.End is null)
            return;


        try
        {
            _stockPrices = (await StockPriceHttpContext.GetStockPrice(Ticker, _dateRange.Start.Value, _dateRange.End.Value, timeSpan)).ToList();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error fetching stock price: {ex.Message}");
        }

        _missingDates = GetMissingDates(_stockPrices, _dateRange.Start.Value, _dateRange.End.Value);
    }

    private List<DateTime> GetMissingDates(List<StockPrice> stockPrices, DateTime start, DateTime end)
    {
        List<DateTime> dateTimes = new List<DateTime>();
        for (var date = start; date <= end; date = date.AddDays(1))
        {
            if (stockPrices.Any(sp => sp.Date.Date == date.Date))
                continue;

            dateTimes.Add(date);
        }

        return dateTimes;
    }
}