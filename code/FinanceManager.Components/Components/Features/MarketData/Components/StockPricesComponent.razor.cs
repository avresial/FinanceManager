using ApexCharts;
using FinanceManager.Application.Identity.Users;
using FinanceManager.Components.HttpClients;
using FinanceManager.Components.Services;
using FinanceManager.Domain.Entities.Stocks;
using FinanceManager.Domain.Identity.Services;
using FinanceManager.Domain.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using MudBlazor;

namespace FinanceManager.Components.Components.Features.MarketData.Components;

public partial class StockPricesComponent
{
    private static readonly ApexChartOptions<StockPrice> _chartOptions = new()
    {
        Chart = new Chart
        {
            Background = "transparent",
            Toolbar = new Toolbar { Show = true, Tools = new Tools { Zoom = true, Pan = true, Reset = true, Download = false } },
            FontFamily = "Roboto, sans-serif",
            Animations = new Animations { Enabled = false },
        },
        Colors = ["#ffab00"],
        Stroke = new Stroke { Curve = Curve.Smooth, Width = 2, LineCap = LineCap.Round },
        DataLabels = new DataLabels { Enabled = false },
        Legend = new Legend { Show = false },
        Fill = new Fill
        {
            Type = [FillType.Gradient],
            Gradient = new FillGradient { OpacityFrom = 0.35, OpacityTo = 0d, Stops = [0, 100] },
        },
        Grid = new Grid
        {
            BorderColor = "rgba(128,128,128,0.18)",
            Xaxis = new GridXAxis { Lines = new Lines { Show = false } },
        },
        Markers = new Markers { Size = 0 },
        Xaxis = new XAxis
        {
            Type = XAxisType.Datetime,
            AxisBorder = new AxisBorder { Show = false },
            AxisTicks = new AxisTicks { Show = false },
            Labels = new XAxisLabels
            {
                Style = new AxisLabelStyle { Colors = "rgba(130,130,130,0.95)", FontSize = "11px" },
                DatetimeFormatter = new DatetimeFormatter { Year = "yyyy", Month = "MMM 'yy", Day = "dd MMM" },
            },
        },
        Yaxis =
        [
            new YAxis
            {
                AxisBorder = new AxisBorder { Show = false },
                AxisTicks = new AxisTicks { Show = false },
                Labels = new YAxisLabels
                {
                    Style = new AxisLabelStyle { Colors = "rgba(130,130,130,0.95)", FontSize = "11px" },
                },
            },
        ],
        Tooltip = new Tooltip
        {
            Theme = Mode.Dark,
            X = new TooltipX { Format = "dd MMM yyyy" },
        },
    };

    private List<StockPrice> _stockPrices = [];
    private List<StockDetails> _allStocks = [];
    private StockDetails? _selectedStock;

    public DateRange DateRange { get; set; } = new(DateTime.UtcNow.AddMonths(-1), DateTime.UtcNow);

    [Parameter] public string Isin { get; set; } = string.Empty;

    [Inject] private StockPriceHttpClient StockPriceHttpClient { get; set; } = default!;
    [Inject] private ISettingsService SettingsService { get; set; } = default!;
    [Inject] private ILogger<StockPricesComponent> Logger { get; set; } = default!;

    protected override async Task OnInitializedAsync()
    {
        try
        {
            _allStocks = await StockPriceHttpClient.GetStocks();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error loading stocks: {Message}", ex.Message);
        }
    }

    private async Task<IEnumerable<StockDetails>> SearchStocks(string value, CancellationToken token)
    {
        if (string.IsNullOrWhiteSpace(value))
            return _allStocks.Take(50);

        var searchValue = value.ToUpperInvariant();
        return await Task.FromResult(_allStocks
            .Where(x => x.Isin.Contains(searchValue, StringComparison.OrdinalIgnoreCase) ||
                       x.Name.Contains(searchValue, StringComparison.OrdinalIgnoreCase))
            .Take(50)
            .ToList());
    }

    private async Task OnStockSelected(StockDetails? stock)
    {
        _selectedStock = stock;

        if (stock is null)
        {
            Isin = string.Empty;
            _stockPrices = [];
            StateHasChanged();
            return;
        }

        Isin = stock.Isin;
        await GetStockPriceAsync();
    }

    private void DateChanged((DateTime Start, DateTime End) dates)
    {
        DateRange = new DateRange(dates.Start, dates.End);

        if (!string.IsNullOrWhiteSpace(Isin))
            _ = GetStockPriceAsync();
    }

    private async Task GetStockPriceAsync()
    {
        TimeSpan timeSpan = TimeSpan.FromDays(1);

        if (string.IsNullOrWhiteSpace(Isin) || DateRange is null || DateRange.Start is null || DateRange.End is null)
            return;

        try
        {
            var currency = SettingsService.GetCurrency();
            _stockPrices = [.. await StockPriceHttpClient.GetStockPrices(Isin, currency.Id, DateRange.Start.Value, DateRange.End.Value, timeSpan)];
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error fetching stock price: {Message}", ex.Message);
        }

        StateHasChanged();
    }
}