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
    public partial class SpendingOverviewCard
    {
        private string currency = "";
        private ApexChart<SpendingOverviewEntry> chart = new ApexChart<SpendingOverviewEntry>();

        private ApexChartOptions<SpendingOverviewEntry> options { get; set; } = new();

        [Parameter]
        public List<SpendingOverviewEntry> Data { get; set; } = new List<SpendingOverviewEntry>();

        [Parameter]
        public DateTime StartDateTime { get; set; }

        [Parameter]
        public string Height { get; set; } = "300px";

        [Inject]
        public required ILogger<SpendingCathegoryOverviewCard> Logger { get; set; }

        [Inject]
        public required IFinancialAccountService FinancalAccountService { get; set; }

        [Inject]
        public required ISettingsService settingsService { get; set; }

        [Inject]
        public required ILoginService loginService { get; set; }


        public decimal TotalSpending = 0;
        protected override async Task OnParametersSetAsync()
        {
            Data.Clear();
            if (chart is not null) await chart.UpdateSeriesAsync(true);
            await Task.Run(async () =>
            {
                var user = await loginService.GetLoggedUser();
                if (user is null) return;
                IEnumerable<BankAccount> bankAccounts = [];
                try
                {
                    bankAccounts = (await FinancalAccountService.GetAccounts<BankAccount>(user.UserId, StartDateTime, DateTime.Now))
                    .Where(x => x.Entries is not null && x.Entries.Count != 0);
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, ex.Message);
                }

                foreach (var account in bankAccounts)
                {
                    if (account.Entries is null) continue;
                    List<FinancialEntryBase> entries = account.Entries.Where(x => x.ValueChange < 0).Select(x => x as FinancialEntryBase).ToList();
                    if ((DateTime.Now - StartDateTime).TotalDays > 6 * 31)
                    {
                        entries = entries.GetEntriesMonthlyValue();
                    }
                    else if ((DateTime.Now - StartDateTime).TotalDays > 31)
                    {
                        entries = entries.GetEntriesWeekly();
                    }

                    foreach (var entry in entries)
                    {
                        var dataEntry = Data.FirstOrDefault(x => x.Date == entry.PostingDate.Date);
                        if (dataEntry is null)
                        {
                            var newDatapoint = new SpendingOverviewEntry()
                            {
                                Date = entry.PostingDate.Date,
                                Value = -entry.ValueChange
                            };
                            Data.Add(newDatapoint);
                        }
                        else
                        {
                            dataEntry.Value += -entry.ValueChange;
                        }
                    }
                    Console.WriteLine("WARNING - If there is gap in data, there will be a gap in chart as well. Add missing days, weeks or months.");
                }

                Data = Data.OrderBy(x => x.Date).ToList();
                TotalSpending = Data.Sum(x => x.Value);
            });

            if (chart is not null) await chart.UpdateSeriesAsync(true);
        }
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
                Type = XAxisType.Datetime

            };

            options.Yaxis = new List<YAxis>();

            options.Yaxis.Add(new YAxis
            {
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
        protected override void OnAfterRender(bool firstRender)
        {
            base.OnAfterRender(firstRender);
        }

        public class SpendingOverviewEntry
        {
            public DateTime Date;
            public decimal Value;
        }

    }
}