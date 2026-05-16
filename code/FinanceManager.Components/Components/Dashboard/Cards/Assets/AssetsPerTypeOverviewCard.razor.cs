using FinanceManager.Components.Models;
using FinanceManager.Components.Services;
using FinanceManager.Domain.Entities.Currencies;
using FinanceManager.Domain.Entities.MoneyFlowModels;
using FinanceManager.Domain.Providers;
using FinanceManager.Domain.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using MudBlazor;
using MudBlazor.Charts;

namespace FinanceManager.Components.Components.Dashboard.Cards.Assets;

public partial class AssetsPerTypeOverviewCard
{
    private readonly ChartOptions _chartOptions = new()
    {
        ChartPalette = ColorsProvider.GetColors().ToArray(),
        ShowLegend = false,
    };

    private bool _isLoading;
    private double[] _data = [];
    private string[] _labels = [];
    private List<ChartSeries<double>> _chartSeries = [];

    private Currency _currency = DefaultCurrency.PLN;
    private decimal _totalAssets = 0;

    [Parameter] public string Height { get; set; } = "300px";
    [Parameter] public DateTime StartDateTime { get; set; }
    [Parameter] public DateTime EndDateTime { get; set; } = DateTime.UtcNow;

    [Inject] public required ILogger<AssetsPerTypeOverviewCard> Logger { get; set; }
    [Inject] public required AssetsPageCardsCacheService AssetsPageCardsCacheService { get; set; }
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
            var user = await LoginService.GetLoggedUser();
            if (user is null)
            {
                _data = [];
                _labels = [];
                _chartSeries = [];
                _totalAssets = 0;
                return;
            }

            var context = new AssetsPageCardsRefreshContext
            {
                UserId = user.UserId,
                CurrencyId = _currency.Id,
                StartDateTime = StartDateTime,
                EndDateTime = EndDateTime,
            };

            var snapshot = await AssetsPageCardsCacheService.GetSnapshotAsync(context);
            var data = snapshot.EndAssetsPerType;
            _totalAssets = data.Count == 0 ? 0 : Math.Round(data.Sum(x => x.Value), 2);
            _data = data.Select(x => (double)x.Value).ToArray();
            _labels = data.Select(x => x.Name).ToArray();
            _chartSeries = [new ChartSeries<double> { Data = _data }];
        }
        catch (Exception ex)
        {
            Logger.LogError(ex.Message, ex);
        }


        _isLoading = false;
        StateHasChanged();
    }
}