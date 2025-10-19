using FinanceManager.Components.HttpContexts;
using FinanceManager.Domain.Entities;
using FinanceManager.Domain.Entities.MoneyFlowModels;
using FinanceManager.Domain.Providers;
using FinanceManager.Domain.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using MudBlazor;

namespace FinanceManager.Components.Components.Dashboard.Cards.Assets;

public partial class AssetsPerTypeOverviewCard
{
    private readonly AxisChartOptions _axisChartOptions = new()
    {
        MatchBoundsToSize = true,
    };
    private readonly ChartOptions _chartOptions = new()
    {
        LineStrokeWidth = 3,
        ChartPalette = ColorsProvider.GetColors().ToArray(),
        ShowLegend = false,
    };

    private bool _isLoading;
    private double[] _data = [];
    private string[] _labels = [];

    private string _currency = "";
    private decimal _totalAssets = 0;

    [Parameter] public string Height { get; set; } = "300px";
    [Parameter] public DateTime StartDateTime { get; set; }
    [Parameter] public DateTime EndDateTime { get; set; } = DateTime.UtcNow;

    [Inject] public required ILogger<AssetsPerTypeOverviewCard> Logger { get; set; }
    [Inject] public required AssetsHttpContext AssetsHttpContext { get; set; }
    [Inject] public required ISettingsService SettingsService { get; set; }
    [Inject] public required ILoginService LoginService { get; set; }

    protected override async Task OnInitializedAsync()
    {
        _currency = SettingsService.GetCurrency();

        await Task.CompletedTask;
    }
    protected override async Task OnParametersSetAsync()
    {
        _isLoading = true;
        StateHasChanged();

        try
        {
            var data = await GetData();
            _data = data.Select(x => (double)x.Value).ToArray();
            _labels = data.Select(x => x.Name).ToArray();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex.Message, ex);
        }


        _isLoading = false;
        StateHasChanged();
    }

    private async Task<List<NameValueResult>> GetData()
    {
        _totalAssets = 0;
        var user = await LoginService.GetLoggedUser();

        if (StartDateTime == new DateTime()) return [];
        if (user is null) return [];

        List<NameValueResult> chartData = [];

        if (user is not null) chartData = await AssetsHttpContext.GetEndAssetsPerType(user.UserId, DefaultCurrency.Currency, StartDateTime, EndDateTime);
        if (chartData.Count != 0) _totalAssets = Math.Round(chartData.Sum(x => x.Value), 2);

        return chartData;
    }
}