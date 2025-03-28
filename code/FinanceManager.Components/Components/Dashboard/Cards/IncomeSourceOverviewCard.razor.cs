using ApexCharts;
using FinanceManager.Components.Helpers;
using FinanceManager.Components.Services;
using FinanceManager.Domain.Entities.Accounts;
using FinanceManager.Domain.Extensions;
using FinanceManager.Domain.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using MudBlazor;

namespace FinanceManager.Components.Components.Dashboard.Cards
{
    public partial class IncomeSourceOverviewCard
    {

        private string currency = string.Empty;
        private ApexChart<IncomeSourceOverviewEntry>? chart;

        private ApexChartOptions<IncomeSourceOverviewEntry> options { get; set; } = new();

        public List<IncomeSourceOverviewEntry> Data { get; set; } = new List<IncomeSourceOverviewEntry>()
        {
            new IncomeSourceOverviewEntry()
            {
                Source = "Sallary",
                Value = 0
            },
        };

        public decimal Total;


        [Inject]
        public required ILogger<IncomeSourceOverviewCard> Logger { get; set; }

        [Inject]
        public required IFinancialAccountService FinancalAccountService { get; set; }

        [Inject]
        public required ISettingsService settingsService { get; set; }

        [Inject]
        public required ILoginService loginService { get; set; }

        [Parameter]
        public string Height { get; set; } = "300px";

        [Parameter]
        public DateTime StartDateTime { get; set; }

        protected override void OnInitialized()
        {
            currency = settingsService.GetCurrency();
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

            options.Yaxis = new List<YAxis>();

            options.Yaxis.Add(new YAxis
            {
                AxisTicks = new AxisTicks()
                {
                    Show = false
                },
                Show = false,
                SeriesName = "income",
                DecimalsInFloat = 0,

            });

            options.Tooltip = new ApexCharts.Tooltip
            {
                Y = new TooltipY
                {
                    Formatter = ChartHelper.GetCurrencyFormatter(settingsService.GetCurrency())
                }
            };

        }


        protected override async Task OnParametersSetAsync()
        {
            var user = await loginService.GetLoggedUser();
            if (user is null) return;

            Data.Clear();
            if (chart is not null) await chart.UpdateSeriesAsync(true);
            await Task.Run(async () =>
            {
                IEnumerable<BankAccount> bankAccounts = [];
                try
                {
                    bankAccounts = (await FinancalAccountService.GetAccounts<BankAccount>(user.UserId, StartDateTime, DateTime.Now))
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
                        var dataEntry = Data.FirstOrDefault();
                        if (dataEntry is null)
                        {
                            Data.Add(new IncomeSourceOverviewEntry()
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
                Total = Data.Sum(x => x.Value);

            });
            if (chart is not null) await chart.UpdateSeriesAsync(true);
        }
        public class IncomeSourceOverviewEntry
        {
            public string Source = string.Empty;
            public decimal Value;
        }

    }
}