using ApexCharts;
using FinanceManager.Components.Helpers;
using FinanceManager.Components.HttpClients;
using FinanceManager.Domain.Entities.Currencies;
using FinanceManager.Domain.Entities.MoneyFlowModels;
using FinanceManager.Domain.Providers;
using FinanceManager.Domain.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;

namespace FinanceManager.Components.Components.Features.Dashboard.Cards.Liabilities;

public partial class LiabilitiesDistributionOverviewCard
{
    private const string _viewByType = "type";
    private const string _viewByAccount = "account";

    private string _view = _viewByType;
    private bool _isLoading;
    private Currency _currency = DefaultCurrency.PLN;
    private List<NameValueResult> _typeData = [];
    private List<NameValueResult> _accountData = [];
    private ApexChart<NameValueResult>? _chart;

    [Parameter] public string Height { get; set; } = "300px";
    [Parameter] public DateTime StartDateTime { get; set; }
    [Parameter] public DateTime EndDateTime { get; set; } = DateTime.UtcNow;

    [Inject] public required ILogger<LiabilitiesDistributionOverviewCard> Logger { get; set; }
    [Inject] public required LiabilitiesHttpClient LiabilitiesHttpClient { get; set; }
    [Inject] public required ISettingsService SettingsService { get; set; }
    [Inject] public required ILoginService LoginService { get; set; }

    private List<NameValueResult> ActiveData => _view == _viewByAccount ? _accountData : _typeData;

    // Derive the total from the active dataset so per-view percentages and the header stay self-consistent.
    private decimal TotalLiabilities => ActiveData.Count == 0 ? 0 : Math.Round(ActiveData.Sum(x => x.Value), 2);

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
            if (StartDateTime == new DateTime())
            {
                _typeData = [];
                _accountData = [];
                return;
            }

            var user = await LoginService.GetLoggedUser();
            if (user is null)
            {
                _typeData = [];
                _accountData = [];
                return;
            }

            var typeTask = LiabilitiesHttpClient.GetEndLiabilitiesPerType(user.UserId, StartDateTime, EndDateTime).ToListAsync().AsTask();
            var accountTask = LiabilitiesHttpClient.GetEndLiabilitiesPerAccount(user.UserId, StartDateTime, EndDateTime).ToListAsync().AsTask();
            await Task.WhenAll(typeTask, accountTask);

            _typeData = ToPositive(await typeTask);
            _accountData = ToPositive(await accountTask);

            if (_chart is not null)
                await _chart.UpdateSeriesAsync(true);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error loading liabilities distribution");
        }
        finally
        {
            _isLoading = false;
            StateHasChanged();
        }
    }

    // Liabilities are returned as negative magnitudes; flip them so the pie and legend show positive amounts.
    private static List<NameValueResult> ToPositive(IEnumerable<NameValueResult> data) =>
        [.. data.Select(x => new NameValueResult { Name = x.Name, Value = Math.Abs(x.Value) })];

    private async Task OnViewChangedAsync(string view)
    {
        _view = view;
        StateHasChanged();
        if (_chart is not null)
            await _chart.UpdateSeriesAsync(true);
    }
}