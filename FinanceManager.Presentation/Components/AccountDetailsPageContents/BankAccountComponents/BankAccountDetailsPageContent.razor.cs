using ApexCharts;
using FinanceManager.Core.Entities.Accounts;
using FinanceManager.Core.Providers;
using FinanceManager.Core.Services;
using FinanceManager.Presentation.Helpers;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace FinanceManager.Presentation.Components.AccountDetailsPageContents.BankAccountComponents
{
    public partial class BankAccountDetailsPageContent : ComponentBase
    {
        private decimal balanceChange = 100;
        private bool LoadedAllData = false;
        private DateTime dateStart;

        private bool ImportFinancialEntriesComponentVisibility;
        private bool AddEntryVisibility;
        private ApexChart<BankAccountEntry> chart;

        internal List<BankAccountEntry>? Top5;
        internal List<BankAccountEntry>? Bottom5;
        internal string currency = "PLN";

        public bool IsLoading = false;
        public BankAccount? Account { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;


        [Parameter]
        public required string AccountName { get; set; }

        [Inject]
        public required IAccountService AccountService { get; set; }

        [Inject]
        public required ISettingsService settingsService { get; set; }

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
            var newData = AccountService.GetAccount<BankAccount>(AccountName, newStartDate, Account.Start.Value);

            if (Account.Entries is null || newData is null || newData.Entries is null || newData.Entries.Count() == 1)
            {
                LoadedAllData = true;
                return;
            }

            var newEntriesWithoutOldest = newData.Entries.Skip(1);
            if (Account.Entries.Any(x => x.Id == newEntriesWithoutOldest.First().Id))
            {
                LoadedAllData = true;
                return;
            }

            Account.Add(newEntriesWithoutOldest, false);

            if ((Account.Entries.Last().PostingDate - newStartDate).TotalDays > 1)
                LoadedAllData = true;

            if (chart is not null)
                await chart.RenderAsync();

            UpdateInfo();
        }

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

            AccountService.AccountsChanged += AccountsService_AccountsChanged;
        }
        protected override async Task OnParametersSetAsync()
        {
            IsLoading = true;
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
            IsLoading = false;
        }

        private async Task UpdateEntries()
        {
            try
            {
                dateStart = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);
                var accounts = AccountService.GetAvailableAccounts();
                if (accounts.ContainsKey(AccountName))
                {
                    var accountType = accounts[AccountName];
                    if (accountType == typeof(BankAccount))
                    {
                        Account = AccountService.GetAccount<BankAccount>(AccountName, dateStart, DateTime.UtcNow);
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
        private void AccountsService_AccountsChanged()
        {
            StateHasChanged();
        }
    }
}