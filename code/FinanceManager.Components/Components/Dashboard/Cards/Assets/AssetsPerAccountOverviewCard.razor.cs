using FinanceManager.Components.Helpers;
using FinanceManager.Components.HttpClients;
using FinanceManager.Domain.Entities.Currencies;
using FinanceManager.Domain.Entities.MoneyFlowModels;
using FinanceManager.Domain.Entities.Users;
using FinanceManager.Domain.Providers;
using FinanceManager.Domain.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using MudBlazor;
using MudBlazor.Charts;

namespace FinanceManager.Components.Components.Dashboard.Cards.Assets;

public partial class AssetsPerAccountOverviewCard
{
    private readonly ChartOptions _chartOptions = new()
    {
        ChartPalette = ColorsProvider.GetColors().ToArray(),
        ShowLegend = false,
        ShowToolTips = true,
    };

    private Currency _currency = DefaultCurrency.PLN;
    private decimal _totalAssets = 0;
    private UserSession? _user;
    private bool _isLoading = false;
    private double[] _chartData = [];
    private string[] _chartLabels = [];
    private List<ChartSeries<double>> _chartSeries = [];

    [Parameter] public bool DisplayAsChart { get; set; } = true;
    [Parameter] public string Height { get; set; } = "300px";
    [Parameter] public DateTime StartDateTime { get; set; }
    [Parameter] public DateTime EndDateTime { get; set; } = DateTime.UtcNow;

    [Inject] public required ILogger<AssetsPerAccountOverviewCard> Logger { get; set; }
    [Inject] public required AssetsHttpClient AssetsHttpClient { get; set; }
    [Inject] public required ISettingsService SettingsService { get; set; }
    [Inject] public required ILoginService LoginService { get; set; }

    public List<NameValueResult> Data { get; set; } = [];


    protected override async Task OnInitializedAsync()
    {
        _currency = SettingsService.GetCurrency();

        await Task.CompletedTask;
    }

    protected override async Task OnParametersSetAsync()
    {
        _isLoading = true;

        try
        {
            _user = await LoginService.GetLoggedUser();
            if (_user is null) return;

            foreach (var dataEntry in Data)
                dataEntry.Value = 0;

            _totalAssets = 0;

            Data = await GetData();
            _chartData = Data.Select(x => (double)Math.Round(x.Value, 2)).ToArray();
            _chartSeries = [new ChartSeries<double> { Data = _chartData }];

            if (Data.Count != 0) _totalAssets = Math.Round(Data.Sum(x => x.Value), 2);

            _chartLabels = Data.Select(x => FormatLegendLabel(x.Name, x.Value, _totalAssets)).ToArray();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex.Message, ex);
        }

        StateHasChanged();

        _isLoading = false;

    }

    async Task<List<NameValueResult>> GetData()
    {
        if (StartDateTime == new DateTime()) return [];

        if (_user is not null)
        {
            try
            {
                return await AssetsHttpClient.GetEndAssetsPerAccount(_user.UserId, DefaultCurrency.PLN, EndDateTime);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex.ToString());
            }
        }

        return [];
    }

    private string FormatLegendLabel(string name, decimal value, decimal total)
    {
        decimal ratio = total == 0 ? 0 : value / total;
        return $"{name}: {value:0.00} {_currency.ShortName} ({ratio * 100m:0.0}%)";
    }
}