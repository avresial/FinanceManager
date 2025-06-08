using FinanceManager.Components.Services;
using FinanceManager.Domain.Entities.Login;
using FinanceManager.Domain.Entities.MoneyFlowModels;
using FinanceManager.Domain.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using MudBlazor;

namespace FinanceManager.Components.Components.Dashboard.Cards;

public partial class IncomeVsSpendingOverviewCard
{
    private UserSession? _user;

    [Parameter] public string Height { get; set; } = "300px";
    [Parameter] public DateTime StartDateTime { get; set; }

    [Inject] public required IMoneyFlowService MoneyFlowService { get; set; }
    [Inject] public required ILogger<IncomeVsSpendingOverviewCard> Logger { get; set; }
    [Inject] public required IFinancialAccountService FinancalAccountService { get; set; }
    [Inject] public required ISettingsService settingsService { get; set; }
    [Inject] public required ILoginService loginService { get; set; }

    public List<IncomeVsSpendingEntry> ChartData { get; set; } = [];

    protected override void OnInitialized()
    {
        //ChartHelper.GetCurrencyFormatter(settingsService.GetCurrency())

        //_options.Colors = new List<string>
        //{
        //    "#B2BF84",
        //    "#D93D3D",
        //};
    }

    private ChartOptions _options = new ChartOptions
    {
        LineStrokeWidth = 3,

        ChartPalette = ["#B2BF84", "#D93D3D", "#ffab00"],
        ShowLegend = false,
    };



    private AxisChartOptions _axisChartOptions = new AxisChartOptions()
    {
        MatchBoundsToSize = true,
    };

    private List<ChartSeries> series = [];
    private ChartSeries? incomeSerie = null;

    protected override async Task OnParametersSetAsync()
    {
        _user = await loginService.GetLoggedUser();
        if (_user is null) return;
        series.Clear();

        series.Add(await GetIncomeSeries());
        series.Add(await GetSpendingSeries());
    }

    private async Task<ChartSeries> GetIncomeSeries()
    {
        TimeSpan timeSeriesStep = new TimeSpan(1, 0, 0, 0);
        DateTime end = DateTime.UtcNow;

        return new()
        {
            Name = "Income",

            Data = (await MoneyFlowService.GetIncome(_user!.UserId, StartDateTime.Date, end, timeSeriesStep)).Select(x => (double)x.Value).ToArray()
        };

    }

    private async Task<ChartSeries> GetSpendingSeries()
    {
        TimeSpan timeSeriesStep = new TimeSpan(1, 0, 0, 0);
        DateTime end = DateTime.UtcNow;

        return new()
        {
            Name = "Spending",
            Data = (await MoneyFlowService.GetSpending(_user!.UserId, StartDateTime.Date, end, timeSeriesStep)).Select(x => (double)x.Value).ToArray()
        };
    }
    private async Task<List<IncomeVsSpendingEntry>> GetData()
    {
        List<IncomeVsSpendingEntry> result = [];
        TimeSpan timeSeriesStep = new TimeSpan(1, 0, 0, 0);
        DateTime end = DateTime.UtcNow;
        if (_user is null) return [];
        List<TimeSeriesModel> income = [];
        List<TimeSeriesModel> spending = [];
        try
        {
            income = await MoneyFlowService.GetIncome(_user.UserId, StartDateTime, end, timeSeriesStep);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error getting income data");
        }

        try
        {
            spending = await MoneyFlowService.GetSpending(_user.UserId, StartDateTime, end, timeSeriesStep);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error getting spending data");
        }

        if (income.Count == 0 && spending.Count == 0) return [];




        for (var date = end; date >= StartDateTime; date = date.Add(-timeSeriesStep))
        {
            //result.Add(new IncomeVsSpendingEntry
            //{
            //    Date = date,
            //    Income = income.FirstOrDefault(x => x.DateTime == date)?.Value ?? 0,
            //    Spending = spending.FirstOrDefault(x => x.DateTime == date)?.Value ?? 0
            //});
        }


        return result;
    }
}


public class IncomeVsSpendingEntry
{
    public DateTime Date;
    public decimal Income;
    public decimal Spending;
    public decimal Balance;
}