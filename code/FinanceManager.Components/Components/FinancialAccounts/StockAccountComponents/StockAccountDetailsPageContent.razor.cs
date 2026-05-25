using FinanceManager.Components.Helpers;
using FinanceManager.Components.HttpClients;
using FinanceManager.Components.Services;
using FinanceManager.Domain.Entities.Currencies;
using FinanceManager.Domain.Entities.MoneyFlowModels;
using FinanceManager.Domain.Entities.Stocks;
using FinanceManager.Domain.Entities.Users;
using FinanceManager.Domain.Services;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace FinanceManager.Components.Components.FinancialAccounts.StockAccountComponents;

public partial class StockAccountDetailsPageContent : ComponentBase
{
    private const int _minimumEntryCount = 50;

    private bool _isLoadingMore = false;
    private decimal? _balanceChange = null;
    private UnrealizedGainLossAccountResult? _unrealizedAccount;
    private Dictionary<string, UnrealizedGainLossInstrumentResult> _unrealizedByTicker = [];
    private bool _loadedAllData = false;
    private DateTime _dateStart;
    private DateTime _dateEnd = DateTime.UtcNow;

    private bool _addEntryVisibility;
    private bool _isEntriesLoading = false;
    private bool _isChartLoading = false;
    private bool _isSidePanelLoading = false;

    private List<(StockAccountEntry, decimal)>? _top5;
    private List<(StockAccountEntry, decimal)>? _bottom5;
    private Currency _currency = DefaultCurrency.PLN;
    private UserSession? _user;

    private Dictionary<StockAccountEntry, StockPrice> _prices = [];
    private List<string> _stocks = [];
    private List<string> _availableStocks = [];

    private decimal? _filterFrom;
    private decimal? _filterTo;
    private DateTime? _filterDateStart;
    private DateTime? _filterDateEnd;


    public StockAccount? Account { get; set; }
    public string ErrorMessage { get; set; } = string.Empty;
    public List<TimeSeriesModel> ChartData { get; set; } = [];


    [Parameter] public required int AccountId { get; set; }

    [Inject] public required AccountDataSynchronizationService AccountDataSynchronizationService { get; set; }
    [Inject] public required IFinancialAccountService FinancialAccountService { get; set; }
    [Inject] public required StockPriceHttpClient StockPriceHttpClient { get; set; }
    [Inject] public required MoneyFlowHttpClient MoneyFlowHttpClient { get; set; }
    [Inject] public required AssetsHttpClient AssetsHttpClient { get; set; }
    [Inject] public required ISettingsService SettingsService { get; set; }
    [Inject] public required ILoginService LoginService { get; set; }
    protected override async Task OnInitializedAsync()
    {
        _user = await LoginService.GetLoggedUser();
        if (_user is null) return;
        _dateStart = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);
        _currency = SettingsService.GetCurrency();

        _isChartLoading = true;
        _isSidePanelLoading = true;
        await LoadEntriesWithSkeleton();
        var availableStocks = await StockPriceHttpClient.GetStocks();
        _availableStocks = availableStocks.Select(x => x.Ticker).ToList();
        AccountDataSynchronizationService.AccountsChanged += AccountDataSynchronizationService_AccountsChanged;
    }

    protected override async Task OnParametersSetAsync()
    {
        if (Account is not null && Account.AccountId == AccountId) return;
        _loadedAllData = false;
        _isChartLoading = true;
        _isSidePanelLoading = true;
        await LoadEntriesWithSkeleton();
    }

    private async Task LoadEntriesWithSkeleton()
    {
        var entriesTask = FetchAccountEntries();
        var delayTask = Task.Delay(2000);
        if (await Task.WhenAny(entriesTask, delayTask) == delayTask)
        {
            _isEntriesLoading = true;
            StateHasChanged();
            await entriesTask;
            _isEntriesLoading = false;
            StateHasChanged();
        }

        if (Account?.Entries is not null)
            await Task.WhenAll(LoadChartAsync(), LoadSidePanelAsync());
    }

    private async Task LoadChartAsync()
    {
        await UpdateChartData();
        if (ChartData is not null && ChartData.Count >= 2)
            _balanceChange = ChartData.Last().Value - ChartData.First().Value;
        _isChartLoading = false;
        StateHasChanged();
    }

    private async Task LoadSidePanelAsync()
    {
        if (Account?.Entries is null)
        {
            _isSidePanelLoading = false;
            StateHasChanged();
            return;
        }
        UpdateLoadStateFromAccount();
        _stocks = Account.GetStoredTickers();
        await ComputeSidePanelDataAsync();
        _isSidePanelLoading = false;
        StateHasChanged();
    }

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
        var updateChartDataTask = UpdateChartData();
        UpdateLoadStateFromAccount();
        _stocks = Account.GetStoredTickers();
        await ComputeSidePanelDataAsync();
        await updateChartDataTask;
        if (ChartData is not null && ChartData.Count >= 2)
            _balanceChange = ChartData.Last().Value - ChartData.First().Value;
    }

    private async Task ComputeSidePanelDataAsync()
    {
        if (Account?.Entries is null) return;

        var priceTasksByTicker = new Dictionary<string, Task<IEnumerable<StockPrice>>>();
        foreach (var ticker in _stocks)
        {
            priceTasksByTicker[ticker] = StockPriceHttpClient.GetStockPrices(ticker, _currency.Id, _dateStart, _dateEnd, TimeSpan.FromDays(1));
        }
        await Task.WhenAll(priceTasksByTicker.Values);

        foreach (var ticker in _stocks)
        {
            var prices = await priceTasksByTicker[ticker];
            Account.Entries.Where(x => x.Ticker.Equals(ticker, StringComparison.OrdinalIgnoreCase)).ToList().ForEach(entry =>
            {
                var price = GetLatestPriceOnOrBefore(prices, entry.PostingDate.Date);
                if (price is not null)
                    _prices.Add(entry, price);
            });
        }

        await UpdateUnrealizedGainLoss();

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
        var loadResult = await RecentEntriesLoader.LoadMoreAsync(
            Account,
            _dateStart,
            _dateEnd,
            CreateLoaderOptions());
        Account = loadResult.Account;
        _dateStart = loadResult.DateStart;

        await UpdateChartData();
        await UpdateInfo();

        _isLoadingMore = false;
    }

    private async Task UpdateEntries()
    {
        await FetchAccountEntries();
        if (Account?.Entries is not null)
            await UpdateInfo();
    }

    private async Task FetchAccountEntries()
    {
        _prices.Clear();

        try
        {
            if (_user is null) return;

            var requestedDateStart = _dateStart;
            _dateEnd = _filterDateEnd.HasValue ? _filterDateEnd.Value.Date.AddDays(1).AddTicks(-1) : DateTime.UtcNow;
            Account = await FinancialAccountService.GetAccount<StockAccount>(_user.UserId, AccountId, requestedDateStart, _dateEnd, _minimumEntryCount);
            UpdateDateStartFromLoadedEntries(requestedDateStart);
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

        if (Account is null || _user is null) return;

        ChartData.AddRange(await MoneyFlowHttpClient.GetClosingBalance(_user.UserId, _currency, _dateStart, _dateEnd, [AccountId]));
    }
    private void UpdateLoadStateFromAccount()
    {
        if (Account is null)
        {
            _loadedAllData = false;
            return;
        }

        _loadedAllData = !Account.NextOlderEntries.Any();
    }

    private async Task UpdateUnrealizedGainLoss()
    {
        _unrealizedAccount = null;
        _unrealizedByTicker.Clear();

        if (_user is null || Account is null) return;

        var asOfDate = DateTime.UtcNow;
        var instrumentResults = await AssetsHttpClient.GetUnrealizedGainLossPerInstrument(_user.UserId, _currency, asOfDate);
        var accountInstrumentResults = instrumentResults.Where(x => x.AccountId == AccountId).ToList();

        foreach (var result in accountInstrumentResults)
            _unrealizedByTicker[result.InstrumentId] = result;

        _unrealizedAccount = BuildUnrealizedAccountResult(accountInstrumentResults, asOfDate);
    }

    private UnrealizedGainLossAccountResult? BuildUnrealizedAccountResult(
        List<UnrealizedGainLossInstrumentResult> accountInstrumentResults,
        DateTime asOfDate)
    {
        if (!accountInstrumentResults.Any()) return null;

        var included = accountInstrumentResults.Where(x => !x.IsExcludedFromTotals).ToList();
        var excludedCount = accountInstrumentResults.Count(x => x.IsExcludedFromTotals);

        var costBasis = included.Sum(x => x.CostBasis);
        var currentValue = included.Sum(x => x.CurrentValue);
        var unrealized = currentValue - costBasis;
        var unrealizedPercent = costBasis == 0 ? 0 : unrealized / costBasis * 100;

        return new UnrealizedGainLossAccountResult(
            AccountId,
            Account?.Name ?? string.Empty,
            costBasis,
            currentValue,
            unrealized,
            unrealizedPercent,
            asOfDate,
            excludedCount
        );
    }

    private UnrealizedGainLossInstrumentResult? GetUnrealizedForTicker(string ticker) =>
        _unrealizedByTicker.TryGetValue(ticker, out var result) ? result : null;

    private StockPrice? GetResolvedPrice(StockAccountEntry entry)
        => _prices.TryGetValue(entry, out var price) ? price : null;

    private UnrealizedGainLossInstrumentResult? GetUnrealizedForEntry(StockAccountEntry entry)
    {
        if (entry.ValueChange <= 0) return null;

        var tickerResult = GetUnrealizedForTicker(entry.Ticker);
        if (tickerResult is null || tickerResult.IsExcludedFromTotals || tickerResult.Quantity <= 0) return null;

        if (!_prices.TryGetValue(entry, out var buyStockPrice) || buyStockPrice.PricePerUnit <= 0) return null;

        var currentPricePerUnit = tickerResult.CurrentValue / tickerResult.Quantity;
        var buyPricePerUnit = buyStockPrice.PricePerUnit;

        var costBasis = entry.ValueChange * buyPricePerUnit;
        var currentValue = entry.ValueChange * currentPricePerUnit;
        var gainLoss = currentValue - costBasis;
        var percent = costBasis == 0 ? 0 : gainLoss / costBasis * 100;

        return new UnrealizedGainLossInstrumentResult(
            tickerResult.AccountId,
            tickerResult.AccountName,
            tickerResult.InstrumentId,
            tickerResult.InstrumentName,
            entry.ValueChange,
            costBasis,
            currentValue,
            gainLoss,
            percent,
            tickerResult.AsOfDate,
            false,
            null
        );
    }

    private void AccountDataSynchronizationService_AccountsChanged()
    {
        Task.Run(async () =>
        {
            await UpdateEntries();
            await InvokeAsync(StateHasChanged);
        });
    }

    private bool HasActiveFilter => _filterFrom.HasValue || _filterTo.HasValue || _filterDateStart.HasValue || _filterDateEnd.HasValue;

    private List<StockAccountEntry> GetFilteredEntries()
    {
        if (Account?.Entries is null) return [];

        IEnumerable<StockAccountEntry> entries = Account.Entries;

        if (_filterFrom.HasValue)
            entries = entries.Where(x => x.ValueChange >= _filterFrom.Value);
        if (_filterTo.HasValue)
            entries = entries.Where(x => x.ValueChange <= _filterTo.Value);
        if (_filterDateStart.HasValue)
            entries = entries.Where(x => x.PostingDate.Date >= _filterDateStart.Value.Date);
        if (_filterDateEnd.HasValue)
            entries = entries.Where(x => x.PostingDate.Date <= _filterDateEnd.Value.Date);

        return entries.OrderByDescending(x => x.PostingDate).ToList();
    }

    private void OnFilterChanged((decimal? From, decimal? To) filter)
    {
        _filterFrom = filter.From;
        _filterTo = filter.To;
    }

    private async Task OnDateFilterChanged((DateTime? From, DateTime? To) filter)
    {
        _filterDateStart = filter.From;
        _filterDateEnd = filter.To;

        var defaultDateStart = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);
        var fallbackStart = Account?.Start?.Date ?? defaultDateStart;
        _dateStart = _filterDateStart ?? (_filterDateEnd.HasValue ? fallbackStart : defaultDateStart);
        _loadedAllData = false;
        await UpdateEntries();
        StateHasChanged();
    }

    private void UpdateDateStartFromLoadedEntries(DateTime requestedDateStart)
    {
        var oldestLoadedEntryDate = Account?.Entries.LastOrDefault()?.PostingDate;
        _dateStart = oldestLoadedEntryDate is not null && oldestLoadedEntryDate.Value < requestedDateStart
            ? oldestLoadedEntryDate.Value
            : requestedDateStart;
    }

    private RecentEntriesLoaderOptions<StockAccount> CreateLoaderOptions() =>
        new()
        {
            LoadByDateRangeAsync = (dateStart, dateEnd) => _user is null
                ? Task.FromResult<StockAccount?>(null)
                : FinancialAccountService.GetAccount<StockAccount>(_user.UserId, AccountId, dateStart, dateEnd),
            GetEntryCount = account => account.Entries.Count,
            GetNextOlderReferenceDate = account => account.NextOlderEntries.Values.Select(x => (DateTime?)x.PostingDate).Max(),
            HasOlderEntries = account => account.NextOlderEntries.Any(),
        };

    private static StockPrice? GetLatestPriceOnOrBefore(IEnumerable<StockPrice> prices, DateTime targetDate)
    {
        var date = targetDate.Date;

        return prices
            .Where(price => price.Date.Date <= date)
            .OrderByDescending(price => price.Date)
            .FirstOrDefault();
    }
}