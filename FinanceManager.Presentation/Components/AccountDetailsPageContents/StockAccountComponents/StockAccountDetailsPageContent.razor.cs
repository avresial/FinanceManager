using ApexCharts;
using FinanceManager.Core.Entities;
using FinanceManager.Core.Entities.Accounts;
using FinanceManager.Core.Providers;
using FinanceManager.Core.Repositories;
using FinanceManager.Core.Services;
using FinanceManager.Presentation.Helpers;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace FinanceManager.Presentation.Components.AccountDetailsPageContents.StockAccountComponents
{
    public partial class StockAccountDetailsPageContent : ComponentBase
    {

        private decimal? balanceChange = null;
        private ApexChart<ChartEntryModel> chart;
        private Dictionary<InvestmentEntry, StockPrice> prices = new();
        private List<string> stocks = new List<string>();
        private bool LoadedAllData = false;
        private DateTime dateStart;
        private DateTime? oldestEntryDate;
        private DateTime? youngestEntryDate;

        private List<ChartEntryModel> pricesDaily;
        private bool visible;

        internal List<(InvestmentEntry, decimal)>? Top5;
        internal List<(InvestmentEntry, decimal)>? Bottom5;
        internal string currency;

        public bool IsLoading = false;
        public InvestmentAccount? Account { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
        public Type accountType;

        [Parameter]
        public required int AccountId { get; set; }

        [Inject]
        public required IAccountService AccountService { get; set; }

        [Inject]
        public required IStockRepository StockRepository { get; set; }

        [Inject]
        public required ISettingsService settingsService { get; set; }

        public async Task ShowOverlay()
        {
            visible = true;
            StateHasChanged();
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

            AccountService.AccountsChanged += AccountsService_AccountsChanged;
        }
        protected override async Task OnParametersSetAsync()
        {
            IsLoading = true;
            currency = settingsService.GetCurrency();

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
                var accounts = AccountService.GetAvailableAccounts();
                if (accounts.ContainsKey(AccountId))
                {
                    accountType = accounts[AccountId];

                    UpdateDates();

                    if (accountType == typeof(InvestmentAccount))
                    {
                        prices.Clear();
                        LoadedAllData = true;
                        Account = AccountService.GetAccount<InvestmentAccount>(AccountId, dateStart, DateTime.UtcNow);

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

            List<(InvestmentEntry, decimal)> orderedByPrice = new List<(InvestmentEntry, decimal)>();
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
            var newData = AccountService.GetAccount<InvestmentAccount>(AccountId, dateStart, Account.Start.Value);

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
            oldestEntryDate = AccountService.GetStartDate(AccountId);
            youngestEntryDate = AccountService.GetEndDate(AccountId);

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
                    // AxisTicks = new AxisTicks()
                    // {
                    //     Show = false
                    // },
                 //   TickAmount = 1,
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