using ApexCharts;
using FinanceManager.Components.Helpers;
using FinanceManager.Domain.Entities;
using FinanceManager.Domain.Entities.Login;
using FinanceManager.Domain.Entities.MoneyFlowModels;
using FinanceManager.Domain.Providers;
using FinanceManager.Domain.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;

namespace FinanceManager.Components.Components.Dashboard.Cards.Assets;

public partial class AssetsPerAccountOverviewCard
{
    private string _currency = "";
    private decimal _totalAssets = 0;
    private UserSession? _user;
    private ApexChart<NameValueResult>? _chart;

    [Parameter] public bool DisplayAsChart { get; set; } = true;
    [Parameter] public string Height { get; set; } = "300px";
    [Parameter] public DateTime StartDateTime { get; set; }
    [Parameter] public DateTime EndDateTime { get; set; } = DateTime.UtcNow;

    [Inject] public required ILogger<AssetsPerAccountOverviewCard> Logger { get; set; }
    [Inject] public required IAssetsService AssetsService { get; set; }
    [Inject] public required ISettingsService SettingsService { get; set; }
    [Inject] public required ILoginService LoginService { get; set; }

    private ApexChartOptions<NameValueResult> Options { get; set; } = new()
    {
        Chart = new Chart
        {
            Toolbar = new Toolbar
            {
                Show = false
            },
        },
        Xaxis = new XAxis()
        {
            AxisTicks = new AxisTicks()
            {
                Show = false,
            },
            AxisBorder = new AxisBorder()
            {
                Show = false
            },
            Position = XAxisPosition.Bottom,
            Type = XAxisType.Category

        },
        Yaxis = new List<YAxis>()
        {

            new YAxis
            {
                AxisTicks = new AxisTicks()
                {
                    Show = false
                },
                Show = false,
                SeriesName = "NetValue",
                DecimalsInFloat = 0,
            }
        },
        Legend = new Legend()
        {
            Position = LegendPosition.Bottom,
        },
        Colors = ColorsProvider.GetColors()
    };

    public List<NameValueResult> Data { get; set; } = new List<NameValueResult>();


    protected override async Task OnInitializedAsync()
    {
        Options.Tooltip = new Tooltip
        {
            Y = new TooltipY
            {
                Formatter = ChartHelper.GetCurrencyFormatter(SettingsService.GetCurrency())
            }
        };

        _currency = SettingsService.GetCurrency();

        await Task.CompletedTask;
    }

    protected override async Task OnParametersSetAsync()
    {
        _user = await LoginService.GetLoggedUser();
        if (_user is null) return;

        foreach (var dataEntry in Data)
            dataEntry.Value = 0;

        if (_chart is not null) await _chart.UpdateSeriesAsync(true);
        _totalAssets = 0;

        Data = await GetData();

        if (Data.Count != 0) _totalAssets = Math.Round(Data.Sum(x => x.Value), 2);

        StateHasChanged();

        if (_chart is not null) await _chart.UpdateSeriesAsync(true);

    }

    async Task<List<NameValueResult>> GetData()
    {
        if (StartDateTime == new DateTime()) return [];

        if (_user is not null)
        {
            try
            {
                return await AssetsService.GetEndAssetsPerAccount(_user.UserId, DefaultCurrency.Currency, StartDateTime, EndDateTime);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex.ToString());
            }
        }

        return [];
    }
}