using FinanceManager.Components.Components.Charts;
using FinanceManager.Components.Services;
using FinanceManager.Domain.Entities;
using FinanceManager.Domain.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using MudBlazor;
using FinanceManager.Components.HttpContexts;

namespace FinanceManager.Components.Components.Dashboard.Cards;

public partial class IncomeVsSpendingOverviewCard
{

    private List<List<ChartJsLineDataPoint>> _series = [];
    private bool _isInitializing = true;
    private bool _isLoading = false;


    [Parameter] public string Height { get; set; } = "300px";
    [Parameter] public DateTime StartDateTime { get; set; }
    [Parameter] public DateTime EndDateTime { get; set; } = DateTime.UtcNow;
    [Parameter] public bool DisplayIncome { get; set; }
    [Parameter] public bool DisplaySpending { get; set; }
    [Parameter] public bool DisplayBalance { get; set; }
    [Parameter] public bool UseOnlyPrimaryColor { get; set; }

    [Inject] public required MoneyFlowHttpContext MoneyFlowHttpContext { get; set; }
    [Inject] public required ILogger<IncomeVsSpendingOverviewCard> Logger { get; set; }
    [Inject] public required IFinancialAccountService FinancalAccountService { get; set; }
    [Inject] public required ISettingsService SettingsService { get; set; }
    [Inject] public required ILoginService LoginService { get; set; }



    protected override async Task OnParametersSetAsync()
    {
        _isLoading = true;
        StateHasChanged();

        var user = await LoginService.GetLoggedUser();
        if (user is null) return;

        var timespanInDays = (EndDateTime - StartDateTime).TotalDays;
        _series.Clear();
        List<TimeSeriesChartSeries> newData = [];
        try
        {
            if (DisplayIncome)
            {
                var incomeData = (await MoneyFlowHttpContext.GetIncome(user.UserId, DefaultCurrency.PLN, StartDateTime.Date, EndDateTime))
                    .OrderBy(x => x.DateTime)
                    .Select(x => new ChartJsLineDataPoint(x.DateTime.ToLocalTime(), x.Value))
                    .ToList();

                _series.Add(incomeData);

            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, ex.Message);
        }

        try
        {
            if (DisplaySpending)
            {
                var incomeData = (await MoneyFlowHttpContext.GetSpending(user.UserId, DefaultCurrency.PLN, StartDateTime.Date, EndDateTime))
                    .OrderBy(x => x.DateTime)
                    .Select(x => new ChartJsLineDataPoint(x.DateTime.ToLocalTime(), x.Value))
                    .ToList();

                _series.Add(incomeData);
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, ex.Message);
        }

        try
        {
            if (DisplayBalance)
            {
                var incomeData = (await MoneyFlowHttpContext.GetBalance(user.UserId, DefaultCurrency.PLN, StartDateTime.Date, EndDateTime))
                      .OrderBy(x => x.DateTime)
                      .Select(x => new ChartJsLineDataPoint(x.DateTime.ToLocalTime(), x.Value))
                      .ToList();

                _series.Add(incomeData);
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