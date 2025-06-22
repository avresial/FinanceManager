using FinanceManager.Components.HttpContexts;
using FinanceManager.Components.Services;
using FinanceManager.Domain.Entities;
using FinanceManager.Domain.Entities.Accounts;
using FinanceManager.Domain.Entities.Accounts.Entries;
using FinanceManager.Domain.Entities.Login;
using FinanceManager.Domain.Entities.MoneyFlowModels;
using FinanceManager.Domain.Services;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace FinanceManager.Components.Components.AccountDetailsPageContents.StockAccountComponents
{
    public partial class StockAccountDetailsPageContent : ComponentBase
    {
        private bool _isLoadingMore = false;
        private decimal? _balanceChange = null;
        private bool _loadedAllData = false;
        private DateTime _dateStart;
        private DateTime _dateEnd = DateTime.UtcNow;
        private DateTime? _oldestEntryDate;
        private DateTime? _youngestEntryDate;

        private bool _addEntryVisibility;

        private List<(StockAccountEntry, decimal)>? _top5;
        private List<(StockAccountEntry, decimal)>? _bottom5;
        private string _currency = "PLN";
        private UserSession? _user;

        private Dictionary<StockAccountEntry, StockPrice> _prices = new();
        private List<string> _stocks = [];


        public bool IsLoading = false;
        public StockAccount? Account { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
        public List<TimeSeriesModel> ChartData { get; set; } = [];


        [Parameter] public required int AccountId { get; set; }

        [Inject] public required AccountDataSynchronizationService AccountDataSynchronizationService { get; set; }
        [Inject] public required IFinancialAccountService FinancialAccountService { get; set; }
        [Inject] public required StockPriceHttpContext stockPriceHttpContext { get; set; }
        [Inject] public required ISettingsService settingsService { get; set; }
        [Inject] public required ILoginService loginService { get; set; }

        public async Task ShowOverlay()
        {
            _addEntryVisibility = true;
            StateHasChanged();

            await Task.CompletedTask;
        }
        public async Task HideOverlay()
        {
            _addEntryVisibility = false;
            await UpdateInfo();

            StateHasChanged();
        }
        public async Task UpdateInfo()
        {
            if (Account is null || Account.Entries is null) return;
            await UpdateDates();
            _stocks = Account.GetStoredTickers();
            foreach (var entry in Account.Entries)
            {
                if (_prices.ContainsKey(entry)) continue;

                var price = await stockPriceHttpContext.GetStockPrice(entry.Ticker, entry.PostingDate);
                if (price is null) continue;

                _prices.Add(entry, price);
            }

            if (Account.Entries is not null && Account.Entries.Any() && _oldestEntryDate is not null)
                _loadedAllData = (_oldestEntryDate >= Account.Entries.Last().PostingDate);

            await UpdateChartData();

            if (ChartData is not null && ChartData.Count >= 2)
                _balanceChange = ChartData.Last().Value - ChartData.First().Value;


            if (Account.Entries is null) return;

            List<(StockAccountEntry, decimal)> orderedByPrice = [];
            foreach (var entry in Account.Entries)
            {
                var price = _prices[entry];
                orderedByPrice.Add(new(entry, entry.ValueChange * price.PricePerUnit));
            }

            orderedByPrice = orderedByPrice.OrderByDescending(x => x.Item2).ToList();
            _top5 = orderedByPrice.Take(5).ToList();
            _bottom5 = orderedByPrice.Skip(Account.Entries.Count - 5).Take(5).OrderBy(x => x.Item2).ToList();
        }
        public async Task LoadMore()
        {
            if (Account is null || Account.Start is null) return;
            if (_user is null) return;

            _isLoadingMore = true;
            _dateStart = _dateStart.AddMonths(-1);

            int entriesCountBeforeUpdate = 0;
            if (Account.Entries is not null) entriesCountBeforeUpdate = Account.Entries.Count();

            Account = await FinancialAccountService.GetAccount<StockAccount>(Account.UserId, AccountId, _dateStart, _dateEnd);

            if (Account is not null && Account.Entries is not null && Account.Entries.Count == entriesCountBeforeUpdate)
            {
                if (Account.NextOlderEntries is not null && Account.NextOlderEntries.Any())
                {
                    _dateStart = Account.NextOlderEntries.First().Value.PostingDate;
                    Account = await FinancialAccountService.GetAccount<StockAccount>(_user.UserId, AccountId, _dateStart, _dateEnd);
                }
            }

            await UpdateChartData();
            await UpdateInfo();

            _isLoadingMore = false;
        }

        protected override async Task OnInitializedAsync()
        {
            _dateStart = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);
            await UpdateEntries();

            AccountDataSynchronizationService.AccountsChanged += AccountDataSynchronizationService_AccountsChanged;
        }
        protected override async Task OnParametersSetAsync()
        {
            IsLoading = true;
            _user = await loginService.GetLoggedUser();
            if (_user is null) return;

            _currency = settingsService.GetCurrency();

            _loadedAllData = true;
            await UpdateEntries();
            IsLoading = false;
        }

        private async Task UpdateEntries()
        {
            _prices.Clear();
            try
            {
                var accounts = await FinancialAccountService.GetAvailableAccounts();
                if (accounts.ContainsKey(AccountId))
                {
                    var accountType = accounts[AccountId];
                    if (accountType == typeof(StockAccount))
                    {
                        await UpdateDates();
                        if (_user is not null)
                        {
                            Account = await FinancialAccountService.GetAccount<StockAccount>(_user.UserId, AccountId, _dateStart, DateTime.UtcNow);
                            if (Account is not null && Account.Entries is not null)
                                await UpdateInfo();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = ex.Message;
                Console.WriteLine(ex);
            }
        }
        private async Task UpdateChartData()
        {
            ChartData.Clear();

            if (Account is null || Account.Entries is null) return;
            Dictionary<DateOnly, decimal> pricesDaily = await Account.GetDailyPrice(stockPriceHttpContext.GetStockPrice);

            ChartData = pricesDaily.Select(x => new TimeSeriesModel() { DateTime = x.Key.ToDateTime(new TimeOnly()), Value = x.Value }).ToList();
        }
        private async Task UpdateDates()
        {
            _oldestEntryDate = await FinancialAccountService.GetStartDate(AccountId);
            _youngestEntryDate = await FinancialAccountService.GetEndDate(AccountId);

            if (_youngestEntryDate is not null && _dateStart > _youngestEntryDate)
                _dateStart = new DateTime(_youngestEntryDate.Value.Date.Year, _youngestEntryDate.Value.Date.Month, 1);
        }

        private void AccountDataSynchronizationService_AccountsChanged()
        {
            Task.Run(async () =>
            {
                await UpdateEntries();
                await InvokeAsync(StateHasChanged);
            });
        }
    }
}