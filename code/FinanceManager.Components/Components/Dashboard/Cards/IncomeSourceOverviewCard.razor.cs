using ApexCharts;
using FinanceManager.Components.Helpers;
using FinanceManager.Components.Services;
using FinanceManager.Domain.Entities.Currencies;
using FinanceManager.Domain.Entities.FinancialAccounts.Currencies;
using FinanceManager.Domain.Entities.Shared.Accounts;
using FinanceManager.Domain.Extensions;
using FinanceManager.Domain.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using MudBlazor;

namespace FinanceManager.Components.Components.Dashboard.Cards
{
    public partial class IncomeSourceOverviewCard
    {
        private Currency _currency = DefaultCurrency.PLN;
        private ApexChart<IncomeSourceOverviewEntry>? _chart;

        private ApexChartOptions<IncomeSourceOverviewEntry> options = new();

        public List<IncomeSourceOverviewEntry> ChartData { get; set; } =
        [
            new IncomeSourceOverviewEntry()
            {
                Source = "Sallary",
                Value = 0
            },
        ];

        public decimal Total;


        [Inject] public required ILogger<IncomeSourceOverviewCard> Logger { get; set; }
        [Inject] public required IFinancialAccountService FinancalAccountService { get; set; }
        [Inject] public required ISettingsService settingsService { get; set; }
        [Inject] public required ILoginService loginService { get; set; }

        [Parameter] public string Height { get; set; } = "300px";
        [Parameter] public DateTime StartDateTime { get; set; }

        protected override void OnInitialized()
        {
            _currency = settingsService.GetCurrency();
            options.Chart = new Chart
            {
                Toolbar = new ApexCharts.Toolbar
                {
                    Show = false
                },

            };


            options.Xaxis = new XAxis()
            {
                AxisTicks = new AxisTicks()
                {
                    Show = false,
                },
                AxisBorder = new AxisBorder()
                {
                    Show = false
                },
                Position = XAxisPosition.Bottom,
                Type = XAxisType.Category

            };

            options.Yaxis =
            [
                new YAxis
                {
                    AxisTicks = new AxisTicks()
                    {
                        Show = false
                    },
                    Show = false,
                    SeriesName = "income",
                    DecimalsInFloat = 0,

                },
            ];

            options.Tooltip = new ApexCharts.Tooltip
            {
                Y = new TooltipY
                {
                    Formatter = ChartHelper.GetCurrencyFormatter(settingsService.GetCurrency().ShortName)
                }
            };

        }
        protected override async Task OnParametersSetAsync()
        {
            var user = await loginService.GetLoggedUser();
            if (user is null) return;

            ChartData.Clear();
            if (_chart is not null) await _chart.UpdateSeriesAsync(true);
            await Task.Run(async () =>
            {
                IEnumerable<CurrencyAccount> bankAccounts = [];
                try
                {
                    bankAccounts = (await FinancalAccountService.GetAccounts<CurrencyAccount>(user.UserId, StartDateTime, DateTime.Now))
                   .Where(x => x.Entries is not null && x.Entries.Any());
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex.ToString());
                }


                foreach (var account in bankAccounts)
                {
                    if (account.Entries is null) continue;

                    List<FinancialEntryBase> entries = account.Entries.Select(x => x as FinancialEntryBase).ToList();
                    if ((DateTime.Now - StartDateTime).TotalDays > 6 * 31)
                    {
                        entries = entries.GetEntriesMonthlyValue();
                    }
                    else if ((DateTime.Now - StartDateTime).TotalDays > 31)
                    {
                        entries = entries.GetEntriesWeekly();
                    }

                    foreach (var entry in entries.Where(x => x.ValueChange > 0))
                    {
                        var dataEntry = ChartData.FirstOrDefault();
                        if (dataEntry is null)
                        {
                            ChartData.Add(new IncomeSourceOverviewEntry()
                            {
                                Source = "Sallary",
                                Value = entry.ValueChange
                            });
                        }
                        else
                        {
                            dataEntry.Value += entry.ValueChange;
                        }

                    }
                }
                Total = ChartData.Sum(x => x.Value);

            });
            if (_chart is not null) await _chart.UpdateSeriesAsync(true);
        }
        public class IncomeSourceOverviewEntry
        {
            public string Source = string.Empty;
            public decimal Value;
        }

    }
}