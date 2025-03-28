using ApexCharts;
using FinanceManager.Components.Helpers;
using FinanceManager.Domain.Entities.Accounts;
using FinanceManager.Domain.Entities.Login;
using FinanceManager.Domain.Extensions;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using MudBlazor;

namespace FinanceManager.Components.Components.Dashboard.Cards
{
    public partial class IncomeVsSpendingOverviewCard
    {
        private UserSession? user;

        private ApexChart<IncomeVsSpendingEntry>? chart;

        private ApexChartOptions<IncomeVsSpendingEntry> options { get; set; } = new()
        {
            Colors = new List<string> { "#00FF00", "#FF0000" },
            Fill = new Fill
            {
                Type = new List<FillType> { FillType.Gradient, FillType.Gradient },
                Gradient = new FillGradient
                {
                    ShadeIntensity = 1,
                    OpacityFrom = 0.2,
                    OpacityTo = 0.9,

                },
            },
        };

        [Parameter]
        public string Height { get; set; } = "300px";

        [Parameter]
        public DateTime StartDateTime { get; set; }

        [Inject]
        public required ILogger<IncomeVsSpendingOverviewCard> Logger { get; set; }

        public List<IncomeVsSpendingEntry> Data { get; set; } = [];

        protected override void OnInitialized()
        {
            options.Tooltip = new ApexCharts.Tooltip
            {
                Y = new TooltipY
                {
                    Formatter = ChartHelper.GetCurrencyFormatter(settingsService.GetCurrency())
                }
            };
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
            options.Colors = new List<string>
            {
                "#B2BF84",
                "#D93D3D",
            };
        }


        protected override async Task OnParametersSetAsync()
        {
            user = await loginService.GetLoggedUser();
            if (user is null) return;

            Data.Clear();
            Data.AddRange(await GetData());

            if (chart is not null) await chart.UpdateSeriesAsync(true);
        }

        private async Task<List<IncomeVsSpendingEntry>> GetData()
        {
            List<IncomeVsSpendingEntry> result = new List<IncomeVsSpendingEntry>();
            await Task.Run(async () =>
            {
                IEnumerable<BankAccount> bankAccounts = [];
                if (user is not null)
                {
                    try
                    {
                        bankAccounts = await FinancalAccountService.GetAccounts<BankAccount>(user.UserId, StartDateTime, DateTime.Now);
                    }
                    catch (Exception)
                    {
                        Logger.LogWarning("Failed to get bank accounts for user {UserId}", user.UserId);
                        bankAccounts = [];
                    }
                }

                foreach (var account in bankAccounts.Where(x => x.Entries is not null && x.Entries.Any()))
                {
                    if (account.Entries is null) continue;

                    List<FinancialEntryBase> incomeEntries = account.Entries.Where(x => x.ValueChange > 0).Select(x => x as FinancialEntryBase).ToList();
                    List<FinancialEntryBase> spendingEntries = account.Entries.Where(x => x.ValueChange < 0).Select(x => x as FinancialEntryBase).ToList();
                    if ((DateTime.Now - StartDateTime).TotalDays > 6 * 31)
                    {
                        incomeEntries = incomeEntries.GetEntriesMonthlyValue();
                        spendingEntries = spendingEntries.GetEntriesMonthlyValue();
                    }
                    else if ((DateTime.Now - StartDateTime).TotalDays > 31)
                    {
                        incomeEntries = incomeEntries.GetEntriesWeekly();
                        spendingEntries = spendingEntries.GetEntriesWeekly();
                    }

                    List<FinancialEntryBase> entries = new();
                    entries.AddRange(incomeEntries);
                    entries.AddRange(spendingEntries);

                    foreach (var entry in entries)
                    {
                        var dataEntry = result.FirstOrDefault(x => x.Date == entry.PostingDate.Date);
                        if (dataEntry is null)
                        {
                            var newDataEntry = new IncomeVsSpendingEntry() { Date = entry.PostingDate.Date };
                            if (entry.ValueChange < 0)
                            {
                                newDataEntry.Spending = -entry.ValueChange;
                            }
                            else
                            {
                                newDataEntry.Income = entry.ValueChange;
                            }

                            result.Add(newDataEntry);
                        }
                        else
                        {
                            if (entry.ValueChange < 0)
                            {
                                dataEntry.Spending -= entry.ValueChange;
                            }
                            else
                            {
                                dataEntry.Income += entry.ValueChange;
                            }
                        }

                    }
                }
            });

            result = result.OrderBy(x => x.Date).ToList();

            return result;
        }


        public class IncomeVsSpendingEntry
        {
            public DateTime Date;
            public decimal Income;
            public decimal Spending;
        }

    }
}