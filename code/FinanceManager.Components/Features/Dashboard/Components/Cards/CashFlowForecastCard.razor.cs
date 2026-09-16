using FinanceManager.Components.Features.Identity.Services;
using FinanceManager.Components.Features.MoneyFlow.HttpClients;
using FinanceManager.Domain.Identity.Services;
using FinanceManager.Domain.MoneyFlow.Entities;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;

namespace FinanceManager.Components.Features.Dashboard.Components.Cards;

public partial class CashFlowForecastCard : ComponentBase
{
    private static readonly int[] _horizons =
    [
        CashFlowForecastHorizons.ThirtyDays,
        CashFlowForecastHorizons.SixtyDays,
        CashFlowForecastHorizons.NinetyDays
    ];

    private CashFlowForecast? _forecast;
    private string _currency = "PLN";
    private bool _isLoading = true;
    private bool _hasError;

    [Parameter] public string Height { get; set; } = "300px";

    [Inject] public required CashFlowForecastHttpClient CashFlowForecastHttpClient { get; set; }
    [Inject] public required ILoginService LoginService { get; set; }
    [Inject] public required ISettingsService SettingsService { get; set; }
    [Inject] public required ILogger<CashFlowForecastCard> Logger { get; set; }

    protected override Task OnInitializedAsync() => LoadData();

    private async Task LoadData()
    {
        _isLoading = true;
        _hasError = false;

        try
        {
            var user = await LoginService.GetLoggedUser();
            if (user is null)
            {
                _forecast = null;
                return;
            }

            var currency = await SettingsService.GetCurrencyAsync();
            _currency = currency.ShortName;
            _forecast = await CashFlowForecastHttpClient.GetAsync(
                user.UserId,
                currency.Id,
                CashFlowForecastHorizons.NinetyDays);
            _hasError = _forecast is null;
        }
        catch (Exception ex)
        {
            _forecast = null;
            _hasError = true;
            Logger.LogError(ex, "Unable to load cash flow forecast.");
        }
        finally
        {
            _isLoading = false;
        }
    }

    private decimal CurrentBalance => _forecast?.ForecastSeries.FirstOrDefault()?.Value ?? 0m;

    private decimal ValueAt(int horizon)
    {
        var targetDate = _forecast?.AsOfDate.AddDays(horizon).Date;
        return _forecast?.ForecastSeries.FirstOrDefault(point => point.DateTime.Date == targetDate)?.Value
            ?? CurrentBalance;
    }

    private string ExpectedSummary
    {
        get
        {
            if (_forecast is null) return string.Empty;

            var inflows = _forecast.ExpectedTransactions.Count(transaction => transaction.Amount > 0);
            var outflows = _forecast.ExpectedTransactions.Count(transaction => transaction.Amount < 0);
            return $"{inflows} expected inflow{(inflows == 1 ? string.Empty : "s")} · "
                + $"{outflows} expected outflow{(outflows == 1 ? string.Empty : "s")}";
        }
    }

    private string FormatMoney(decimal value) => $"{value:N2} {_currency}";
}