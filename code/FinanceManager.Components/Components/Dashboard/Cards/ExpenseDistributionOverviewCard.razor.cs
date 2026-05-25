using FinanceManager.Components.HttpClients;
using FinanceManager.Domain.Entities.Currencies;
using FinanceManager.Domain.Providers;
using FinanceManager.Domain.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using MudBlazor;
using MudBlazor.Charts;

namespace FinanceManager.Components.Components.Dashboard.Cards;

public partial class ExpenseDistributionOverviewCard
{
    private ChartOptions _chartOptions = new()
    {
        ChartPalette = ColorsProvider.GetColors().ToArray(),
        ShowLegend = false,
    };

    private bool _isLoading;
    private string[] _labels = [];
    private List<ChartSeries<double>> _chartSeries = [];

    private Currency _currency = DefaultCurrency.PLN;
    private decimal _totalExpenses = 0;

    [Parameter] public string Height { get; set; } = "300px";
    [Parameter] public DateTime StartDateTime { get; set; }
    [Parameter] public DateTime EndDateTime { get; set; } = DateTime.UtcNow;

    [Inject] public required ILogger<ExpenseDistributionOverviewCard> Logger { get; set; }
    [Inject] public required MoneyFlowHttpClient MoneyFlowHttpClient { get; set; }
    [Inject] public required ISettingsService SettingsService { get; set; }
    [Inject] public required ILoginService LoginService { get; set; }

    protected override async Task OnInitializedAsync()
    {
        _currency = SettingsService.GetCurrency();
        _chartOptions = new ChartOptions
        {
            ChartPalette = ColorsProvider.GetColors().ToArray(),
            ShowLegend = false,
            TooltipTitleFormat = "{{Y_VALUE}} " + _currency.ShortName,
        };
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
                _labels = [];
                _chartSeries = [];
                _totalExpenses = 0;
                return;
            }

            var data = await MoneyFlowHttpClient.GetExpenseDistribution(user.UserId, _currency, StartDateTime, EndDateTime);

            _totalExpenses = data.Count == 0 ? 0 : Math.Round(data.Sum(x => x.Value), 2);
            var values = data.Select(x => (double)x.Value).ToArray();
            _labels = data.Select((x, i) =>
            {
                var pct = _totalExpenses > 0 ? (double)x.Value / (double)_totalExpenses * 100.0 : 0.0;
                return $"{x.Name} ({pct:F1}%)";
            }).ToArray();
            _chartSeries = [new ChartSeries<double> { Data = values }];
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, ex.Message);
        }
        finally
        {
            _isLoading = false;
            StateHasChanged();
        }
    }
}