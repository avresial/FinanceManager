using ApexCharts;
using FinanceManager.Components.Services;
using FinanceManager.Domain.Entities.Accounts;
using FinanceManager.Domain.Entities.Login;
using FinanceManager.Domain.Providers;
using FinanceManager.Domain.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using MudBlazor;

namespace FinanceManager.Components.Components.Dashboard.Cards
{
    public partial class LiabilityOverviewCard
    {
        private string currency = "";
        private decimal TotalAssets = 0;
        private UserSession? user;



        private ApexChart<LiabilityOverviewEntry>? chart;

        private ApexChartOptions<LiabilityOverviewEntry> options { get; set; } = new()
        {
            Chart = new Chart
            {
                Toolbar = new ApexCharts.Toolbar
                {
                    Show = false
                },
            },
            Xaxis = new XAxis()
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

            },
            Yaxis = new List<YAxis>()
            {

                new YAxis
                {
                    AxisTicks = new AxisTicks()
                    {
                        Show = false
                    },
                    Show = false,
                    SeriesName = "NetValue",
                    DecimalsInFloat = 0,
                }
            },
            Legend = new Legend()
            {
                Position = LegendPosition.Bottom,
            },
            Colors = ColorsProvider.GetColors()
        };

        [Parameter]
        public string Height { get; set; } = "300px";

        [Parameter]
        public DateTime StartDateTime { get; set; }


        [Inject]
        public required ILogger<LiabilityOverviewCard> Logger { get; set; }

        [Inject]
        public required IFinancalAccountService FinancalAccountService { get; set; }

        [Inject]
        public required ISettingsService settingsService { get; set; }

        [Inject]
        public required ILoginService loginService { get; set; }

        public List<LiabilityOverviewEntry> Data { get; set; } = [];


        protected override void OnInitialized()
        {
            currency = settingsService.GetCurrency();
        }


        protected override async Task OnParametersSetAsync()
        {
            user = await loginService.GetLoggedUser();
            if (user is null) return;

            foreach (var dataEntry in Data)
                dataEntry.Value = 0;

            if (chart is not null) await chart.UpdateSeriesAsync(true);

            await GetData();
            StateHasChanged();

            if (chart is not null) await chart.UpdateSeriesAsync(true);
        }

        async Task GetData()
        {
            await Task.Run(() =>
            {
                IEnumerable<BankAccount> bankAccounts = [];
                if (user is not null)
                {
                    try
                    {
                        bankAccounts = FinancalAccountService.GetAccounts<BankAccount>(user.UserId, StartDateTime, DateTime.Now);
                    }
                    catch (Exception e)
                    {
                        Logger.LogError(e, "Error while getting bank accounts");
                    }
                }

                bankAccounts = bankAccounts.Where(x => x.Entries is not null && x.Entries.Any() && x.Entries.First().Value <= 0).ToList();
                TotalAssets = bankAccounts.Sum(x => x.Entries!.OrderByDescending(x => x.PostingDate).First().Value);

                foreach (var account in bankAccounts)
                {
                    var dataEntry = Data.FirstOrDefault(x => x.Cathegory == account.AccountType.ToString());

                    if (dataEntry is not null)
                    {
                        dataEntry.Value -= account.Entries!.First().Value;
                    }
                    else
                    {
                        Data.Add(new LiabilityOverviewEntry()
                        {
                            Cathegory = account.AccountType.ToString(),
                            Value = -account.Entries!.First().Value
                        });
                    }
                }
            });
        }

        public class LiabilityOverviewEntry
        {
            public string Cathegory = string.Empty;
            public decimal Value;
        }
    }
}