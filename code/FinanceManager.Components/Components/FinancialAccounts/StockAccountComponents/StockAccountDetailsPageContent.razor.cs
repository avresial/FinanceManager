using FinanceManager.Application.Services;
using FinanceManager.Components.HttpClients;
using FinanceManager.Components.Services;
using FinanceManager.Domain.Entities.Currencies;
using FinanceManager.Domain.Entities.MoneyFlowModels;
using FinanceManager.Domain.Entities.Stocks;
using FinanceManager.Domain.Entities.Users;
using FinanceManager.Domain.Services;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace FinanceManager.Components.Components.FinancialAccounts.StockAccountComponents
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
        private Currency _currency = DefaultCurrency.PLN;
        private UserSession? _user;

        private Dictionary<StockAccountEntry, StockPrice> _prices = [];
        private List<string> _stocks = [];
        private List<string> _availableStocks = [];

        private decimal? _filterFrom;
        private decimal? _filterTo;


        public bool IsLoading = false;
        public StockAccount? Account { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
        public List<TimeSeriesModel> ChartData { get; set; } = [];


        [Parameter] public required int AccountId { get; set; }

        [Inject] public required AccountDataSynchronizationService AccountDataSynchronizationService { get; set; }
        [Inject] public required IFinancialAccountService FinancialAccountService { get; set; }
        [Inject] public required StockPriceHttpClient StockPriceHttpClient { get; set; }
        [Inject] public required ISettingsService SettingsService { get; set; }
        [Inject] public required ILoginService LoginService { get; set; }

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

                var price = await StockPriceHttpClient.GetStockPrice(entry.Ticker, DefaultCurrency.PLN.Id, entry.PostingDate);
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
                decimal price = entry.ValueChange;
                if (_prices.ContainsKey(entry))
                {
                    StockPrice stockPrice = _prices[entry];
                    price = entry.ValueChange * stockPrice.PricePerUnit;
                }

                orderedByPrice.Add(new(entry, price));
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
            _user = await LoginService.GetLoggedUser();
            if (_user is null) return;
            _dateStart = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);
            _currency = SettingsService.GetCurrency();

            var loadTask = UpdateEntries();
            var delayTask = Task.Delay(2000);
            var completedTask = await Task.WhenAny(loadTask, delayTask);
            if (completedTask == delayTask)
            {
                IsLoading = true;
                StateHasChanged();
                await loadTask;
                IsLoading = false;
            }
            var availableStocks = await StockPriceHttpClient.GetStocks();
            _availableStocks = availableStocks.Select(x => x.Ticker).ToList();
            AccountDataSynchronizationService.AccountsChanged += AccountDataSynchronizationService_AccountsChanged;
        }
        protected override async Task OnParametersSetAsync()
        {
            if (Account is not null && Account.AccountId == AccountId) return;
            _loadedAllData = false;
            var loadTask = UpdateEntries();
            var delayTask = Task.Delay(2000);
            var completedTask = await Task.WhenAny(loadTask, delayTask);
            if (completedTask == delayTask)
            {
                IsLoading = true;
                StateHasChanged();
                await loadTask;
                IsLoading = false;
            }
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
            Dictionary<DateOnly, decimal> pricesDaily = await Account.GetDailyPrice((ticker, date) => StockPriceHttpClient.GetStockPrice(ticker, _currency.Id, date));

            var result = pricesDaily.Select(x => new TimeSeriesModel() { DateTime = x.Key.ToDateTime(new TimeOnly()), Value = x.Value }).ToList();
            var timeBucket = TimeBucketService.Get(result.Select(x => (x.DateTime, x.Value)));
            ChartData.AddRange(timeBucket.Select(x => new TimeSeriesModel()
            {
                DateTime = x.Date,
                Value = x.Objects.Last(),
            }));
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

        private bool HasActiveFilter => _filterFrom.HasValue || _filterTo.HasValue;

        private List<StockAccountEntry> GetFilteredEntries()
        {
            if (Account?.Entries is null) return [];

            IEnumerable<StockAccountEntry> entries = Account.Entries;

            if (_filterFrom.HasValue)
                entries = entries.Where(x => x.ValueChange >= _filterFrom.Value);
            if (_filterTo.HasValue)
                entries = entries.Where(x => x.ValueChange <= _filterTo.Value);

            return entries.OrderByDescending(x => x.PostingDate).ToList();
        }

        private void OnFilterChanged((decimal? From, decimal? To) filter)
        {
            _filterFrom = filter.From;
            _filterTo = filter.To;
        }
    }
}