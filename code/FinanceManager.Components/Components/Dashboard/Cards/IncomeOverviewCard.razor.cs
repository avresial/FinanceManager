using ApexCharts;
using FinanceManager.Components.Helpers;
using FinanceManager.Components.Services;
using FinanceManager.Domain.Entities.Accounts;
using FinanceManager.Domain.Extensions;
using FinanceManager.Domain.Providers;
using FinanceManager.Domain.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using MudBlazor;

namespace FinanceManager.Components.Components.Dashboard.Cards
{
    public partial class IncomeOverviewCard
    {
        private string currency = string.Empty;
        private ApexChart<SpendingOverviewEntry>? chart;

        private ApexChartOptions<SpendingOverviewEntry> options { get; set; } = new();

        public List<SpendingOverviewEntry> Data { get; set; } = [];

        [Inject]
        public required ILogger<IncomeOverviewCard> Logger { get; set; }
        [Inject]
        public required IFinancalAccountService FinancalAccountService { get; set; }
        [Inject]
        public required ISettingsService settingsService { get; set; }
        [Inject]
        public required ILoginService loginService { get; set; }

        [Parameter]
        public string Height { get; set; } = "300px";

        [Parameter]
        public DateTime StartDateTime { get; set; }

        public decimal TotalIncome = 0;

        protected override void OnInitialized()
        {
            currency = settingsService.GetCurrency();
            options.Chart = new Chart
            {
                Toolbar = new ApexCharts.Toolbar
                {
                    Show = false
                },
                Zoom = new Zoom()
                {
                    Enabled = false
                },
                Sparkline = new ChartSparkline()
                {
                    Enabled = true,
                }
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
                Type = XAxisType.Datetime

            };

            options.Yaxis = new List<YAxis>();

            options.Yaxis.Add(new YAxis
            {
                TickAmount = 2,
                AxisTicks = new AxisTicks()
                {
                    Show = false
                },
                Show = false,
                SeriesName = "NetValue",
                DecimalsInFloat = 0,

            });
            options.Tooltip = new ApexCharts.Tooltip
            {
                Y = new TooltipY
                {
                    Formatter = ChartHelper.GetCurrencyFormatter(settingsService.GetCurrency())
                }
            };
            options.Colors = new List<string>
            {
                ColorsProvider.GetColors().First()
            };

        }
        private async Task<List<SpendingOverviewEntry>> GetData()
        {
            var result = new List<SpendingOverviewEntry>();
            await Task.Run(async () =>
            {
                var user = await loginService.GetLoggedUser();
                if (user is null) return;

                IEnumerable<BankAccount> bankAccounts = [];

                try
                {
                    bankAccounts = FinancalAccountService.GetAccounts<BankAccount>(user.UserId, StartDateTime, DateTime.Now);
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex.ToString());
                }

                foreach (var account in bankAccounts)
                {
                    if (account.Entries is null) continue;
                    var entries = account.Entries.Where(x => x.ValueChange >= 0).Select(x => x as FinancialEntryBase).ToList();
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
                        var dataEntry = result.FirstOrDefault(x => x.Date == entry.PostingDate.Date);
                        if (dataEntry is null)
                        {
                            result.Add(new SpendingOverviewEntry()
                            {
                                Date = entry.PostingDate.Date,
                                Value = entry.ValueChange
                            });
                        }
                        else
                        {
                            dataEntry.Value += entry.ValueChange;
                        }

                    }
                }
                TotalIncome = result.Sum(x => x.Value);
            });
            return result.OrderBy(x => x.Date).ToList();
        }

        protected override async Task OnParametersSetAsync()
        {
            Data.Clear();
            if (chart is not null) await chart.UpdateSeriesAsync(true);

            Data.AddRange(await GetData());

            if (chart is not null) await chart.UpdateSeriesAsync(true);
        }
        public class SpendingOverviewEntry
        {
            public DateTime Date;
            public decimal Value;
        }
    }
}