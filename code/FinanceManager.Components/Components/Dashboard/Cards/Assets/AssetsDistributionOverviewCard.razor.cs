using ApexCharts;
using FinanceManager.Components.Helpers;
using FinanceManager.Components.Models;
using FinanceManager.Components.Services;
using FinanceManager.Domain.Entities.Currencies;
using FinanceManager.Domain.Entities.MoneyFlowModels;
using FinanceManager.Domain.Providers;
using FinanceManager.Domain.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;

namespace FinanceManager.Components.Components.Dashboard.Cards.Assets;

public partial class AssetsDistributionOverviewCard
{
    private const string _viewByType = "type";
    private const string _viewByWallet = "wallet";

    private string _view = _viewByType;
    private bool _isLoading;
    private Currency _currency = DefaultCurrency.PLN;
    private decimal _totalAssets;
    private List<NameValueResult> _typeData = [];
    private List<NameValueResult> _walletData = [];
    private ApexChart<NameValueResult>? _chart;

    [Parameter] public string Height { get; set; } = "300px";
    [Parameter] public DateTime StartDateTime { get; set; }
    [Parameter] public DateTime EndDateTime { get; set; } = DateTime.UtcNow;

    [Inject] public required ILogger<AssetsDistributionOverviewCard> Logger { get; set; }
    [Inject] public required AssetsPageCardsCacheService AssetsPageCardsCacheService { get; set; }
    [Inject] public required ISettingsService SettingsService { get; set; }
    [Inject] public required ILoginService LoginService { get; set; }

    private List<NameValueResult> ActiveData => _view == _viewByWallet ? _walletData : _typeData;

    private readonly ApexChartOptions<NameValueResult> _chartOptions = new()
    {
        Chart = new Chart
        {
            Toolbar = new Toolbar { Show = false },
            Background = "transparent",
        },
        Legend = new Legend { Show = false },
        Colors = ColorsProvider.GetColors(),
    };

    protected override void OnInitialized()
    {
        _currency = SettingsService.GetCurrency();
        _chartOptions.Tooltip = new Tooltip
        {
            Y = new TooltipY
            {
                Formatter = ChartHelper.GetCurrencyFormatter(_currency.ShortName),
            },
        };
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
                _typeData = [];
                _walletData = [];
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
            _typeData = [.. snapshot.EndAssetsPerType];
            _walletData = [.. snapshot.EndAssetsPerAccount];
            _totalAssets = _typeData.Count == 0 ? 0 : Math.Round(_typeData.Sum(x => x.Value), 2);

            if (_chart is not null)
                await _chart.UpdateSeriesAsync(true);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error loading assets distribution");
        }
        finally
        {
            _isLoading = false;
            StateHasChanged();
        }
    }

    private async Task OnViewChangedAsync(string view)
    {
        _view = view;
        StateHasChanged();
        if (_chart is not null)
            await _chart.UpdateSeriesAsync(true);
    }
}
