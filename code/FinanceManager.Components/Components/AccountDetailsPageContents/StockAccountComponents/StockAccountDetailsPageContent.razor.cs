using ApexCharts;
using FinanceManager.Components.Helpers;
using FinanceManager.Components.Services;
using FinanceManager.Domain.Entities;
using FinanceManager.Domain.Entities.Accounts;
using FinanceManager.Domain.Entities.Accounts.Entries;
using FinanceManager.Domain.Entities.Login;
using FinanceManager.Domain.Entities.MoneyFlowModels;
using FinanceManager.Domain.Providers;
using FinanceManager.Domain.Repositories;
using FinanceManager.Domain.Services;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace FinanceManager.Components.Components.AccountDetailsPageContents.StockAccountComponents
{
    public partial class StockAccountDetailsPageContent : ComponentBase
    {

        private decimal? _balanceChange = null;
        private ApexChart<TimeSeriesModel>? _chart;
        private Dictionary<StockAccountEntry, StockPrice> _prices = new();
        private List<string> _stocks = [];
        private bool _loadedAllData;
        private DateTime _dateStart;
        private DateTime _dateEnd = DateTime.UtcNow;
        private DateTime? _oldestEntryDate;
        private DateTime? _youngestEntryDate;
        private UserSession? _user;
        private List<TimeSeriesModel> _pricesDaily = [];
        private bool _visible;
        private List<(StockAccountEntry, decimal)>? _top5;
        private List<(StockAccountEntry, decimal)>? _bottom5;
        private string _currency = string.Empty;

        public bool IsLoading;
        public StockAccount? Account { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
        public Type? accountType;
        public List<TimeSeriesModel> ChartData { get; set; } = [];

        [Parameter] public required int AccountId { get; set; }

        [Inject] public required AccountDataSynchronizationService AccountDataSynchronizationService { get; set; }
        [Inject] public required IFinancialAccountService FinancalAccountService { get; set; }
        [Inject] public required IStockRepository StockRepository { get; set; }
        [Inject] public required ISettingsService settingsService { get; set; }
        [Inject] public required ILoginService loginService { get; set; }

        public async Task ShowOverlay()
        {
            _visible = true;
            StateHasChanged();

            await Task.CompletedTask;
        }

        public async Task HideOverlay()
        {
            _visible = false;
            await UpdateInfo();

            if (_chart is not null) await _chart.RenderAsync();

            StateHasChanged();
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

            AccountDataSynchronizationService.AccountsChanged += AccountsService_AccountsChanged;
        }
        protected override async Task OnParametersSetAsync()
        {
            IsLoading = true;
            _currency = settingsService.GetCurrency();
            _user = await loginService.GetLoggedUser();
            if (_user is null) return;
            if (_chart is not null)
            {
                if (Account is not null && Account.Entries is not null)
                    Account.Entries.Clear();

                await _chart.RenderAsync();
            }

            _loadedAllData = true;
            await UpdateEntries();
            IsLoading = false;
        }
        private async Task UpdateEntries()
        {
            try
            {
                _dateStart = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);
                var accounts = await FinancalAccountService.GetAvailableAccounts();
                if (accounts.ContainsKey(AccountId))
                {
                    accountType = accounts[AccountId];

                    await UpdateDates();

                    if (accountType == typeof(StockAccount))
                    {
                        _prices.Clear();
                        _loadedAllData = true;
                        if (_user is not null)
                            Account = await FinancalAccountService.GetAccount<StockAccount>(_user.UserId, AccountId, _dateStart, DateTime.UtcNow);

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
            await UpdateDates();
            _stocks = Account.GetStoredTickers();
            foreach (var entry in Account.Entries)
            {
                if (_prices.ContainsKey(entry)) continue;

                var price = await StockRepository.GetStockPrice(entry.Ticker, entry.PostingDate);
                _prices.Add(entry, price);
            }

            if (Account.Entries is not null && Account.Entries.Any() && _oldestEntryDate is not null)
                _loadedAllData = (_oldestEntryDate >= Account.Entries.Last().PostingDate);

            await UpdateChartData();

            if (_pricesDaily is not null && _pricesDaily.Count >= 2)
                _balanceChange = _pricesDaily.Last().Value - _pricesDaily.First().Value;

            if (_chart is not null) await _chart.RenderAsync();

            if (Account.Entries is null) return;

            List<(StockAccountEntry, decimal)> orderedByPrice = new List<(StockAccountEntry, decimal)>();
            foreach (var entry in Account.Entries)
            {
                var price = await StockRepository.GetStockPrice(entry.Ticker, entry.PostingDate);
                orderedByPrice.Add(new(entry, entry.ValueChange * price.PricePerUnit));
            }

            orderedByPrice = orderedByPrice.OrderByDescending(x => x.Item2).ToList();
            _top5 = orderedByPrice.Take(5).ToList();
            _bottom5 = orderedByPrice.Skip(Account.Entries.Count - 5).Take(5).OrderBy(x => x.Item2).ToList();
        }

        public async Task LoadMore()
        {
            if (Account is null || Account.Start is null) return;

            _dateStart = _dateStart.AddMonths(-1);
            var newData = await FinancalAccountService.GetAccount<StockAccount>(Account.UserId, AccountId, _dateStart, Account.Start.Value);

            if (Account.Entries is null || newData is null || newData.Entries is null || newData.Entries.Count() == 1)
                return;

            var newEntriesWithoutOldest = newData.Entries.Skip(1);

            Account.Add(newEntriesWithoutOldest, false);
            if (_oldestEntryDate is not null)
                _loadedAllData = (_oldestEntryDate >= Account.Entries.Last().PostingDate);

            await UpdateInfo();
        }
        private async Task UpdateChartData()
        {
            _pricesDaily.Clear();

            if (Account is null || Account.Entries is null) return;
            Dictionary<DateOnly, decimal> pricesDaily = await Account.GetDailyPrice(StockRepository.GetStockPrice);
            decimal previousValue = 0;

            for (DateTime date = _dateStart; date <= _dateEnd; date = date.AddDays(1))
            {
                decimal? value = null;
                var dateOnly = DateOnly.FromDateTime(date.Date);

                if (pricesDaily.ContainsKey(dateOnly))
                    value = pricesDaily[DateOnly.FromDateTime(date.Date)];

                TimeSeriesModel timeSeriesModel = new()
                {
                    DateTime = date,
                    Value = value ?? previousValue,
                };

                if (value is not null) previousValue = value.Value;

                _pricesDaily.Add(timeSeriesModel);
            }
        }
        private async Task UpdateDates()
        {
            _oldestEntryDate = await FinancalAccountService.GetStartDate(AccountId);
            _youngestEntryDate = await FinancalAccountService.GetEndDate(AccountId);

            if (_youngestEntryDate is not null && _dateStart > _youngestEntryDate)
                _dateStart = new DateTime(_youngestEntryDate.Value.Date.Year, _youngestEntryDate.Value.Date.Month, 1);
        }
        private ApexChartOptions<TimeSeriesModel> options { get; set; } = new()
        {
            Chart = new Chart
            {
                Sparkline = new()
                {
                    Enabled = true,
                },
                Toolbar = new()
                {
                    Show = false
                },
            },
            Xaxis = new()
            {
                AxisTicks = new()
                {
                    Show = false,
                },
                AxisBorder = new()
                {
                    Show = false
                },
            },
            Yaxis =
            [
                new()
                {
                    Show = false,
                    SeriesName = "Vaue",
                    DecimalsInFloat = 0,
                }
            ],
            Colors = [ColorsProvider.GetColors().First()]
        };

        private void AccountsService_AccountsChanged()
        {
            if (_chart is not null)
                _ = UpdateInfo();

            StateHasChanged();
        }
    }
}