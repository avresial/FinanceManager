using FinanceManager.Components.Components.SharedComponents.Charts;
using FinanceManager.Components.HttpClients;
using FinanceManager.Components.Services;
using FinanceManager.Domain.Entities.Currencies;
using FinanceManager.Domain.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using MudBlazor;

namespace FinanceManager.Components.Components.Dashboard.Cards;

public partial class InflowVsOutflowOverviewCard
{

    private List<List<ChartJsLineDataPoint>> _series = [];
    private bool _isInitializing = true;
    private bool _isLoading = false;


    [Parameter] public string Height { get; set; } = "300px";
    [Parameter] public DateTime StartDateTime { get; set; }
    [Parameter] public DateTime EndDateTime { get; set; } = DateTime.UtcNow;
    [Parameter] public bool DisplayInflow { get; set; }
    [Parameter] public bool DisplayOutflow { get; set; }
    [Parameter] public bool DisplayClosingBalance { get; set; }
    [Parameter] public bool UseOnlyPrimaryColor { get; set; }

    [Inject] public required MoneyFlowHttpClient MoneyFlowHttpClient { get; set; }
    [Inject] public required ILogger<InflowVsOutflowOverviewCard> Logger { get; set; }
    [Inject] public required IFinancialAccountService FinancalAccountService { get; set; }
    [Inject] public required ISettingsService SettingsService { get; set; }
    [Inject] public required ILoginService LoginService { get; set; }



    protected override async Task OnParametersSetAsync()
    {
        _isLoading = true;
        StateHasChanged();

        var user = await LoginService.GetLoggedUser();
        if (user is null) return;

        _series.Clear();
        try
        {
            if (DisplayInflow)
            {
                var flowData = (await MoneyFlowHttpClient.GetInflow(user.UserId, DefaultCurrency.PLN, StartDateTime.Date, EndDateTime))
                    .OrderBy(x => x.DateTime)
                    .Select(x => new ChartJsLineDataPoint(x.DateTime.ToLocalTime(), x.Value))
                    .ToList();

                _series.Add(flowData);

            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, ex.Message);
        }

        try
        {
            if (DisplayOutflow)
            {
                var flowData = (await MoneyFlowHttpClient.GetOutflow(user.UserId, DefaultCurrency.PLN, StartDateTime.Date, EndDateTime))
                    .OrderBy(x => x.DateTime)
                    .Select(x => new ChartJsLineDataPoint(x.DateTime.ToLocalTime(), x.Value))
                    .ToList();

                _series.Add(flowData);
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, ex.Message);
        }

        try
        {
            if (DisplayClosingBalance)
            {
                var flowData = (await MoneyFlowHttpClient.GetClosingBalance(user.UserId, DefaultCurrency.PLN, StartDateTime.Date, EndDateTime))
                      .OrderBy(x => x.DateTime)
                      .Select(x => new ChartJsLineDataPoint(x.DateTime.ToLocalTime(), x.Value))
                      .ToList();

                _series.Add(flowData);
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, ex.Message);
        }

        _isLoading = false;
        _isInitializing = false;
    }
}