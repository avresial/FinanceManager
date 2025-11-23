using ApexCharts;
using FinanceManager.Components.Helpers;
using FinanceManager.Components.HttpClients;
using FinanceManager.Domain.Entities.Currencies;
using FinanceManager.Domain.Entities.MoneyFlowModels;
using FinanceManager.Domain.Entities.Users;
using FinanceManager.Domain.Providers;
using FinanceManager.Domain.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;

namespace FinanceManager.Components.Components.Dashboard.Cards.Liabilities;

public partial class LiabilitiesPerAccountOverviewCard
{
    private Currency _currency = DefaultCurrency.PLN;
    private decimal _totalLiabilities = 0;
    private UserSession? _user;
    private ApexChart<NameValueResult>? _chart;

    [Parameter] public bool DisplayAsChart { get; set; } = true;
    [Parameter] public string Height { get; set; } = "300px";
    [Parameter] public DateTime StartDateTime { get; set; }
    [Parameter] public DateTime EndDateTime { get; set; } = DateTime.UtcNow;


    [Inject] public required ILogger<LiabilitiesPerAccountOverviewCard> Logger { get; set; }
    [Inject] public required LiabilitiesHttpClient LiabilitiesHttpClient { get; set; }
    [Inject] public required ISettingsService SettingsService { get; set; }
    [Inject] public required ILoginService LoginService { get; set; }

    private ApexChartOptions<NameValueResult> _options { get; set; } = new()
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
        _options.Tooltip = new Tooltip
        {
            Y = new TooltipY
            {
                Formatter = ChartHelper.GetCurrencyFormatter(SettingsService.GetCurrency().ShortName)
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

        await GetData();

        StateHasChanged();

        if (_chart is not null) await _chart.UpdateSeriesAsync(true);

    }

    async Task GetData()
    {
        Data.Clear();
        _totalLiabilities = 0;

        if (StartDateTime == new DateTime())
            return;

        if (_user is not null)
        {
            try
            {
                Data.AddRange(await LiabilitiesHttpClient.GetEndLiabilitiesPerAccount(_user.UserId, StartDateTime, EndDateTime).ToListAsync());
            }
            catch (Exception ex)
            {
                Logger.LogError(ex.ToString());
            }
        }

        if (Data.Count != 0)
            _totalLiabilities = Data.Sum(x => x.Value);

        foreach (var data in Data)
            data.Value *= -1;
    }

}