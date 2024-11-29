using ApexCharts;
using FinanceManager.Core.Entities.Accounts;
using FinanceManager.Core.Providers;
using FinanceManager.Core.Repositories;
using FinanceManager.Core.Services;
using FinanceManager.Presentation.Helpers;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace FinanceManager.Presentation.Components.AccountDetailsPageContents
{
    public partial class BankAccountDetailsPageContent : ComponentBase
    {

        private decimal balanceChange = 100;
        private bool LoadedAllData = false;
        private DateTime dateStart;

        internal List<BankAccountEntry>? Top5;
        internal List<BankAccountEntry>? Bottom5;
        internal string currency = "PLN";

        private ApexChart<BankAccountEntry> chart;
        public BankAccount? Account { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;

        private bool AddEntryVisibility;

        public async Task ShowOverlay()
        {
            AddEntryVisibility = true;
            StateHasChanged();
        }

        public async Task HideOverlay()
        {
            AddEntryVisibility = false;
            UpdateInfo();
            if (chart is not null)
                await chart.RenderAsync();
            StateHasChanged();
        }

        [Parameter]
        public required string AccountName { get; set; }

        [Inject]
        public IFinancalAccountRepository BankAccountRepository { get; set; }

        [Inject]
        public ISettingsService settingsService { get; set; }

        protected override async Task OnInitializedAsync()
        {
            options.Tooltip = new Tooltip
            {
                Y = new TooltipY
                {
                    Formatter = ChartHelper.GetCurrencyFormatter(settingsService.GetCurrency())
                }
            };

            await UpdateEntries();
        }

        protected override async Task OnParametersSetAsync()
        {
            if (chart is not null)
            {
                if (Account is not null && Account.Entries is not null)
                    Account.Entries.Clear();

                await chart.RenderAsync();
            }

            LoadedAllData = false;
            await UpdateEntries();

            if (chart is not null)
            {
                StateHasChanged();
                await chart.RenderAsync();
            }
        }

        public Type accountType;

        private async Task UpdateEntries()
        {
            try
            {
                dateStart = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);
                var accounts = BankAccountRepository.GetAvailableAccounts();
                if (accounts.ContainsKey(AccountName))
                {
                    accountType = accounts[AccountName];
                    if (accountType == typeof(BankAccount))
                    {
                        Account = BankAccountRepository.GetAccount<BankAccount>(AccountName, dateStart, DateTime.UtcNow);
                        if (Account is not null && Account.Entries is not null)
                            UpdateInfo();
                    }
                }

            }
            catch (Exception ex)
            {
                ErrorMessage = ex.Message;
            }
        }

        public void UpdateInfo()
        {
            if (Account is null || Account.Entries is null) return;
            var EntriesOrdered = Account.Entries.OrderByDescending(x => x.ValueChange);
            Top5 = EntriesOrdered.Take(5).ToList();
            Bottom5 = EntriesOrdered.Skip(Account.Entries.Count - 5).Take(5).OrderBy(x => x.ValueChange).ToList();
            balanceChange = Account.Entries.First().Value - Account.Entries.Last().Value;

            if (Account.Entries.Last().PostingDate.Date > dateStart.Date)
                LoadedAllData = true;
        }

        public async Task LoadMore()
        {
            if (Account is null || Account.Start is null) return;

            var newStartDate = Account.Start.Value.AddMonths(-1);
            var newData = BankAccountRepository.GetAccount<BankAccount>(AccountName, newStartDate, Account.Start.Value);

            if (Account.Entries is null || newData is null || newData.Entries is null) return;
            if (!newData.Entries.Any() || newData.Entries.Last().PostingDate == Account.Entries.Last().PostingDate)
            {
                LoadedAllData = true;
                return;
            }

            var lastExisting = Account.Entries.LastOrDefault();
            var firstNew = newData.Entries.FirstOrDefault();

            Account.Add(newData.Entries.Skip(1), false);

            if (chart is not null)
                await chart.RenderAsync();

            UpdateInfo();
        }


        private ApexChartOptions<BankAccountEntry> options { get; set; } = new()
        {
            Chart = new Chart
            {
                Sparkline = new ChartSparkline()
                {
                    Enabled = true,
                },
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
            },
            Yaxis = new List<YAxis>()
            {
                new YAxis
                {
                    Show = false,
                    SeriesName = "Vaue",
                    DecimalsInFloat = 0,
                }
            },
            Colors = new List<string>
            {
               ColorsProvider.GetColors().First()
            }
        };
    }
}