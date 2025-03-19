using ApexCharts;
using FinanceManager.Components.Helpers;
using FinanceManager.Components.Services;
using FinanceManager.Domain.Entities;
using FinanceManager.Domain.Entities.Accounts;
using FinanceManager.Domain.Entities.Accounts.Entries;
using FinanceManager.Domain.Entities.Login;
using FinanceManager.Domain.Providers;
using FinanceManager.Domain.Repositories;
using FinanceManager.Domain.Services;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace FinanceManager.Components.Components.AccountDetailsPageContents.StockAccountComponents
{
    public partial class StockAccountDetailsPageContent : ComponentBase
    {

        private decimal? balanceChange = null;
        private ApexChart<ChartEntryModel>? chart;
        private Dictionary<StockAccountEntry, StockPrice> prices = new();
        private List<string> stocks = new List<string>();
        private bool LoadedAllData = false;
        private DateTime dateStart;
        private DateTime? oldestEntryDate;
        private DateTime? youngestEntryDate;
        private UserSession? user;

        private List<ChartEntryModel> pricesDaily = new();
        private bool visible;

        internal List<(StockAccountEntry, decimal)>? Top5;
        internal List<(StockAccountEntry, decimal)>? Bottom5;
        internal string currency = string.Empty;

        public bool IsLoading = false;
        public StockAccount? Account { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
        public Type? accountType;

        [Parameter]
        public required int AccountId { get; set; }
        [Inject]
        public required AccountDataSynchronizationService AccountDataSynchronizationService { get; set; }
        [Inject]
        public required IFinancalAccountService FinancalAccountService { get; set; }

        [Inject]
        public required IStockRepository StockRepository { get; set; }

        [Inject]
        public required ISettingsService settingsService { get; set; }
        [Inject]
        public required ILoginService loginService { get; set; }

        public async Task ShowOverlay()
        {
            visible = true;
            StateHasChanged();

            await Task.CompletedTask;
        }

        public async Task HideOverlay()
        {
            visible = false;
            await UpdateInfo();
            if (chart is not null)
                await chart.RenderAsync();
            StateHasChanged();
        }

        protected override async Task OnInitializedAsync()
        {
            options.Tooltip = new ApexCharts.Tooltip
            {
                Y = new TooltipY
                {
                    Formatter = ChartHelper.GetCurrencyFormatter(settingsService.GetCurrency())
                }
            };

            await UpdateEntries();

            AccountDataSynchronizationService.AccountsChanged += AccountsService_AccountsChanged;
        }
        protected override async Task OnParametersSetAsync()
        {
            IsLoading = true;
            currency = settingsService.GetCurrency();
            user = await loginService.GetLoggedUser();
            if (user is null) return;
            if (chart is not null)
            {
                if (Account is not null && Account.Entries is not null)
                    Account.Entries.Clear();

                await chart.RenderAsync();
            }

            LoadedAllData = true;
            await UpdateEntries();
            IsLoading = false;
        }
        private async Task UpdateEntries()
        {
            try
            {
                dateStart = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);
                var accounts = FinancalAccountService.GetAvailableAccounts();
                if (accounts.ContainsKey(AccountId))
                {
                    accountType = accounts[AccountId];

                    UpdateDates();

                    if (accountType == typeof(StockAccount))
                    {
                        prices.Clear();
                        LoadedAllData = true;
                        if (user is not null)
                            Account = FinancalAccountService.GetAccount<StockAccount>(user.UserId, AccountId, dateStart, DateTime.UtcNow);

                        if (Account is not null && Account.Entries is not null)
                            await UpdateInfo();
                    }
                }

            }
            catch (Exception ex)
            {
                ErrorMessage = ex.Message;
                Console.WriteLine(ex);
            }
        }
        public async Task UpdateInfo()
        {
            if (Account is null || Account.Entries is null) return;
            UpdateDates();
            stocks = Account.GetStoredTickers();
            foreach (var entry in Account.Entries)
            {
                if (prices.ContainsKey(entry)) continue;

                var price = await StockRepository.GetStockPrice(entry.Ticker, entry.PostingDate);
                prices.Add(entry, price);
            }

            if (Account.Entries is not null && Account.Entries.Any() && oldestEntryDate is not null)
                LoadedAllData = (oldestEntryDate >= Account.Entries.Last().PostingDate);

            pricesDaily = (await Account.GetDailyPrice(StockRepository.GetStockPrice))
                                        .Select(x => new ChartEntryModel() { Date = x.Key.ToDateTime(new TimeOnly()), Value = x.Value })
                                        .ToList();

            if (pricesDaily is not null && pricesDaily.Count >= 2)
                balanceChange = pricesDaily.Last().Value - pricesDaily.First().Value;

            if (chart is not null) await chart.RenderAsync();

            if (Account.Entries is null) return;

            List<(StockAccountEntry, decimal)> orderedByPrice = new List<(StockAccountEntry, decimal)>();
            foreach (var entry in Account.Entries)
            {
                var price = await StockRepository.GetStockPrice(entry.Ticker, entry.PostingDate);
                orderedByPrice.Add(new(entry, entry.ValueChange * price.PricePerUnit));
            }

            orderedByPrice = orderedByPrice.OrderByDescending(x => x.Item2).ToList();
            Top5 = orderedByPrice.Take(5).ToList();
            Bottom5 = orderedByPrice.Skip(Account.Entries.Count - 5).Take(5).OrderBy(x => x.Item2).ToList();
        }

        public async Task LoadMore()
        {
            if (Account is null || Account.Start is null) return;

            dateStart = dateStart.AddMonths(-1);
            var newData = FinancalAccountService.GetAccount<StockAccount>(Account.UserId, AccountId, dateStart, Account.Start.Value);

            if (Account.Entries is null || newData is null || newData.Entries is null || newData.Entries.Count() == 1)
                return;

            var newEntriesWithoutOldest = newData.Entries.Skip(1);

            Account.Add(newEntriesWithoutOldest, false);
            if (oldestEntryDate is not null)
                LoadedAllData = (oldestEntryDate >= Account.Entries.Last().PostingDate);

            await UpdateInfo();
        }

        private void UpdateDates()
        {
            oldestEntryDate = FinancalAccountService.GetStartDate(AccountId);
            youngestEntryDate = FinancalAccountService.GetEndDate(AccountId);

            if (youngestEntryDate is not null && dateStart > youngestEntryDate)
                dateStart = new DateTime(youngestEntryDate.Value.Date.Year, youngestEntryDate.Value.Date.Month, 1);
        }
        private ApexChartOptions<ChartEntryModel> options { get; set; } = new()
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
            if (chart is not null)
                _ = UpdateInfo();

            StateHasChanged();
        }

    }
}