using ApexCharts;
using FinanceManager.Components.Services;
using FinanceManager.Domain.Entities.Accounts;
using FinanceManager.Domain.Entities.Login;
using FinanceManager.Domain.Entities.MoneyFlowModels;
using FinanceManager.Domain.Providers;
using FinanceManager.Domain.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using MudBlazor;

namespace FinanceManager.Components.Components.Dashboard.Cards.Liabilities
{
    public partial class LiabilityOverviewCard
    {
        private string _currency = "";
        private decimal _totalLiabilities = 0;
        private UserSession? _user;
        private ApexChart<PieChartModel>? _chart;
        private ApexChartOptions<PieChartModel> _options { get; set; } = new()
        {
            Chart = new Chart
            {
                Toolbar = new()
                {
                    Show = false
                },
            },
            Legend = new Legend()
            {
                Position = LegendPosition.Bottom,
            },
            Colors = ColorsProvider.GetColors()
        };

        [Parameter] public string Height { get; set; } = "300px";
        [Parameter] public DateTime StartDateTime { get; set; }


        [Inject] public required ILogger<LiabilityOverviewCard> Logger { get; set; }
        [Inject] public required IFinancialAccountService FinancalAccountService { get; set; }
        [Inject] public required ISettingsService SettingsService { get; set; }
        [Inject] public required ILoginService LoginService { get; set; }

        public List<PieChartModel> ChartData { get; set; } = [];

        protected override void OnInitialized()
        {
            _currency = SettingsService.GetCurrency();
        }
        protected override async Task OnParametersSetAsync()
        {
            _user = await LoginService.GetLoggedUser();
            if (_user is null) return;

            foreach (var dataEntry in ChartData)
                dataEntry.Value = 0;

            if (_chart is not null) await _chart.UpdateSeriesAsync();

            await GetData();
            StateHasChanged();

            if (_chart is not null) await _chart.UpdateSeriesAsync();
        }

        private async Task GetData()
        {
            await Task.Run(async () =>
            {
                IEnumerable<BankAccount> bankAccounts = [];
                if (_user is not null)
                {
                    try
                    {
                        bankAccounts = await FinancalAccountService.GetAccounts<BankAccount>(_user.UserId, StartDateTime, DateTime.Now);
                    }
                    catch (Exception e)
                    {
                        Logger.LogError(e, "Error while getting bank accounts");
                    }
                }

                bankAccounts = bankAccounts.Where(x => x.Entries is not null && x.Entries.Any() && x.Entries.First().Value <= 0).ToList();
                _totalLiabilities = bankAccounts.Sum(x => x.Entries!.OrderByDescending(x => x.PostingDate).First().Value);

                foreach (var account in bankAccounts)
                {
                    var dataEntry = ChartData.FirstOrDefault(x => x.Name == account.AccountType.ToString());

                    if (dataEntry is not null)
                    {
                        dataEntry.Value += Math.Abs(account.Entries!.First().Value);
                    }
                    else
                    {
                        ChartData.Add(new PieChartModel()
                        {
                            Name = account.AccountType.ToString(),
                            Value = -account.Entries!.First().Value,
                        });
                    }
                }
            });

            if (_chart is not null) await _chart.UpdateSeriesAsync();
        }

    }
}