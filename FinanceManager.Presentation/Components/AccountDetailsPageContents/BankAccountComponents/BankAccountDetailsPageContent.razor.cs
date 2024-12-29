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
        private DateTime? oldestEntryDate;
        private DateTime? youngestEntryDate;

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
        public required int AccountId { get; set; }

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

            UpdateDates();

            if (Account.Entries is not null && Account.Entries.Any() && oldestEntryDate is not null)
                LoadedAllData = (oldestEntryDate >= Account.Entries.Last().PostingDate);

            var EntriesOrdered = Account.Entries.OrderByDescending(x => x.ValueChange);
            Top5 = EntriesOrdered.Take(5).ToList();
            Bottom5 = EntriesOrdered.Skip(Account.Entries.Count - 5).Take(5).OrderBy(x => x.ValueChange).ToList();
            balanceChange = Account.Entries.First().Value - Account.Entries.Last().Value;
        }
        public async Task LoadMore()
        {
            if (Account is null || Account.Start is null) return;

            dateStart = dateStart.AddMonths(-1);
            var newData = AccountService.GetAccount<BankAccount>(AccountId, dateStart, Account.Start.Value);

            if (Account.Entries is null || newData is null || newData.Entries is null || newData.Entries.Count() == 1)
                return;

            var newEntriesWithoutOldest = newData.Entries.Skip(1);
            Account.Add(newEntriesWithoutOldest, false);

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
                if (accounts.ContainsKey(AccountId))
                {
                    var accountType = accounts[AccountId];
                    if (accountType == typeof(BankAccount))
                    {
                        UpdateDates();
                        Account = AccountService.GetAccount<BankAccount>(AccountId, dateStart, DateTime.UtcNow);
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
        private void UpdateDates()
        {
            oldestEntryDate = AccountService.GetStartDate(AccountId);
            youngestEntryDate = AccountService.GetEndDate(AccountId);

            if (youngestEntryDate is not null && dateStart > youngestEntryDate)
                dateStart = new DateTime(youngestEntryDate.Value.Date.Year, youngestEntryDate.Value.Date.Month, 1);
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