using FinanceManager.Components.Components.FinancialAccounts.CurrencyAccountComponents;
using FinanceManager.Components.HttpClients;
using FinanceManager.Components.Services;
using FinanceManager.Domain.Entities.Currencies;
using FinanceManager.Domain.Entities.MoneyFlowModels;
using FinanceManager.Domain.Entities.Stocks;
using FinanceManager.Domain.Entities.Users;
using FinanceManager.Domain.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using MudBlazor;

namespace FinanceManager.Components.Components.FinancialAccounts.StockAccountComponents;

public partial class StockAccountDetailsPageContent : ComponentBase, IAsyncDisposable
{
    private readonly Guid _viewportSubscriptionId = Guid.NewGuid();
    private bool _isMobile;
    private bool _insightsDrawerOpen;

    private string _selectedRange = "3M";
    private DateTime _dateStart;
    private DateTime _dateEnd = DateTime.UtcNow;
    private DateRange? _customDateRange;

    private bool _addEntryVisibility;

    private string? _searchText;
    private AccountHistoryToolbar.TxFilter? _activeFilter;
    private string? _selectedCategory;
    private IEnumerable<string> _availableCategories = [];

    private decimal _currentBalance;
    private decimal _balanceChange;
    private decimal? _balanceChangePercent;
    private List<StockAccountEntry>? _top5;
    private List<StockAccountEntry>? _bottom5;
    private Currency _currency = DefaultCurrency.PLN;
    private readonly string _accountTypeLabel = "Stock account";
    private List<string> _availableStocks = [];
    private UserSession? _user;

    public bool IsLoading = false;
    public StockAccount? Account { get; set; }
    public string ErrorMessage { get; set; } = string.Empty;
    public List<TimeSeriesModel> ChartData { get; set; } = [];

    [Parameter] public required int AccountId { get; set; }

    [Inject] public required IFinancialAccountService FinancialAccountService { get; set; }
    [Inject] public required AccountDataSynchronizationService AccountDataSynchronizationService { get; set; }
    [Inject] public required ISettingsService SettingsService { get; set; }
    [Inject] public required ILoginService LoginService { get; set; }
    [Inject] public required MoneyFlowHttpClient MoneyFlowHttpClient { get; set; }
    [Inject] public required StockPriceHttpClient StockPriceHttpClient { get; set; }
    [Inject] public required ILogger<StockAccountDetailsPageContent> Logger { get; set; }
    [Inject] public required IBrowserViewportService BrowserViewportService { get; set; }

    public Task ShowAddOverlay()
    {
        _addEntryVisibility = true;
        StateHasChanged();
        return Task.CompletedTask;
    }

    private void ToggleInsightsDrawer() => _insightsDrawerOpen = !_insightsDrawerOpen;

    private void CloseInsightsDrawer() => _insightsDrawerOpen = false;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender) return;

        await BrowserViewportService.SubscribeAsync(_viewportSubscriptionId, async args =>
        {
            var isMobile = args.Breakpoint is Breakpoint.Xs or Breakpoint.Sm or Breakpoint.Md;
            if (isMobile == _isMobile) return;
            _isMobile = isMobile;
            if (!_isMobile) _insightsDrawerOpen = false;
            await InvokeAsync(StateHasChanged);
        }, fireImmediately: true);
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            await BrowserViewportService.UnsubscribeAsync(_viewportSubscriptionId);
        }
        catch (Exception ex)
        {
            Logger.LogDebug(ex, "Failed to unsubscribe from BrowserViewportService");
        }
        AccountDataSynchronizationService.AccountsChanged -= AccountDataSynchronizationService_AccountsChanged;
        GC.SuppressFinalize(this);
    }

    public Task HideAddOverlay()
    {
        _addEntryVisibility = false;
        StateHasChanged();
        return Task.CompletedTask;
    }

    public async Task UpdateInfo(bool refreshChart = true)
    {
        if (Account is null || Account.Entries is null) return;

        if (Account.Entries.Count == 0)
        {
            _currentBalance = 0;
            _balanceChange = 0;
            _balanceChangePercent = null;
            _top5 = [];
            _bottom5 = [];
            return;
        }

        var filteredEntries = GetFilteredEntries();

        var entriesOrdered = filteredEntries.OrderByDescending(x => x.ValueChange).ToList();
        _top5 = entriesOrdered.Where(x => x.ValueChange > 0).Take(5).ToList();
        _bottom5 = entriesOrdered.Where(x => x.ValueChange < 0)
                                 .OrderBy(x => x.ValueChange)
                                 .Take(5)
                                 .ToList();

        // Text filters (search/income-expense/category) narrow the list and movers only;
        // the chart and hero balance track the selected date range, so skip the network
        // refetch when only a filter changed.
        if (refreshChart)
            await UpdateChartData();

        // Stock entry Value/ValueChange are unit-denominated, so the hero balance and
        // change come from the currency-denominated closing-balance series (the same data
        // the chart plots) rather than from the entries.
        _currentBalance = ChartData.LastOrDefault()?.Value ?? 0;
        _balanceChange = ChartData.Count >= 2 ? ChartData.Last().Value - ChartData.First().Value : 0;

        var startBalance = _currentBalance - _balanceChange;
        _balanceChangePercent = startBalance == 0 ? null : _balanceChange / startBalance * 100m;

        _availableCategories = Account.Entries
            .SelectMany(e => e.Labels ?? [])
            .Where(l => l is not null)
            .Select(l => l.Name)
            .Distinct()
            .OrderBy(n => n)
            .ToList();
    }

    protected override async Task OnInitializedAsync()
    {
        try
        {
            _user = await LoginService.GetLoggedUser();
            if (_user is null)
            {
                IsLoading = false;
                return;
            }

            SetDateRangeForSelection();

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
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error during initialization of StockAccountDetailsPageContent for account ID {AccountId}", AccountId);
        }
    }

    protected override async Task OnParametersSetAsync()
    {
        try
        {
            if (Account is not null && Account.AccountId == AccountId) return;
            IsLoading = true;
            SetDateRangeForSelection();
            await UpdateEntries();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error during initialization of StockAccountDetailsPageContent for account ID {AccountId}", AccountId);
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task UpdateEntries()
    {
        try
        {
            if (_user is null) return;

            // Preset ranges always end "now"; a custom RANGE keeps the end picked in the hero
            // (already set by SetDateRangeForSelection), so don't overwrite it here.
            if (_selectedRange != "Range")
                _dateEnd = DateTime.UtcNow;
            Account = await FinancialAccountService.GetAccount<StockAccount>(_user.UserId, AccountId, _dateStart, _dateEnd);

            if (Account?.Entries is not null)
                await UpdateInfo();
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            Logger.LogError(ex, "Error while loading stock account details for account ID {AccountId}", AccountId);
        }
    }

    private async Task UpdateChartData()
    {
        ChartData.Clear();
        if (Account is null || _user is null) return;

        _currency = SettingsService.GetCurrency();
        var chartData = await MoneyFlowHttpClient.GetClosingBalance(_user.UserId, _currency, _dateStart, _dateEnd, [AccountId]);
        ChartData.AddRange(chartData.SkipWhile(x => x.Value == 0));
    }

    private void AccountDataSynchronizationService_AccountsChanged()
    {
        _ = InvokeAsync(async () =>
        {
            try
            {
                await UpdateEntries();
                StateHasChanged();
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error while refreshing stock account details after data sync for account ID {AccountId}", AccountId);
            }
        });
    }

    private async Task OnRangeChanged(string value)
    {
        _selectedRange = value;
        SetDateRangeForSelection();
        IsLoading = true;
        StateHasChanged();
        try
        {
            await UpdateEntries();
        }
        finally
        {
            IsLoading = false;
            StateHasChanged();
        }
    }

    private async Task OnCustomDateRangeChanged(DateRange? range)
    {
        _customDateRange = range;
        if (_selectedRange != "Range") return;
        SetDateRangeForSelection();
        IsLoading = true;
        StateHasChanged();
        try
        {
            await UpdateEntries();
        }
        finally
        {
            IsLoading = false;
            StateHasChanged();
        }
    }

    private async Task OnSearchChanged(string? value)
    {
        _searchText = value;
        await UpdateInfo(refreshChart: false);
        StateHasChanged();
    }

    private async Task OnTxFilterChanged(AccountHistoryToolbar.TxFilter? value)
    {
        _activeFilter = value;
        await UpdateInfo(refreshChart: false);
        StateHasChanged();
    }

    private async Task OnCategoryChanged(string? value)
    {
        _selectedCategory = value;
        await UpdateInfo(refreshChart: false);
        StateHasChanged();
    }

    private bool HasActiveFilter =>
        !string.IsNullOrWhiteSpace(_searchText) || _activeFilter.HasValue || _selectedCategory is not null;

    private List<StockAccountEntry> GetFilteredEntries()
    {
        if (Account?.Entries is null) return [];

        IEnumerable<StockAccountEntry> entries = Account.Entries;

        if (_activeFilter == AccountHistoryToolbar.TxFilter.Income)
            entries = entries.Where(x => x.ValueChange > 0);
        else if (_activeFilter == AccountHistoryToolbar.TxFilter.Expense)
            entries = entries.Where(x => x.ValueChange < 0);

        if (!string.IsNullOrWhiteSpace(_selectedCategory))
            entries = entries.Where(x => x.Labels is not null
                && x.Labels.Any(l => string.Equals(l.Name, _selectedCategory, StringComparison.OrdinalIgnoreCase)));

        if (!string.IsNullOrWhiteSpace(_searchText))
        {
            var needle = _searchText.Trim();
            entries = entries.Where(x =>
                (!string.IsNullOrEmpty(x.Ticker) && x.Ticker.Contains(needle, StringComparison.OrdinalIgnoreCase))
                || (!string.IsNullOrEmpty(x.Isin) && x.Isin.Contains(needle, StringComparison.OrdinalIgnoreCase))
                || (x.Labels is not null && x.Labels.Any(l => l.Name.Contains(needle, StringComparison.OrdinalIgnoreCase))));
        }

        return entries.OrderByDescending(x => x.PostingDate).ToList();
    }

    private void SetDateRangeForSelection()
    {
        var today = DateTime.UtcNow;
        if (_selectedRange == "Range")
        {
            _dateStart = _customDateRange?.Start ?? Account?.Start ?? today.AddMonths(-3);
            _dateEnd = _customDateRange?.End ?? today;
            return;
        }

        _dateStart = _selectedRange switch
        {
            "1M" => today.AddMonths(-1),
            "3M" => today.AddMonths(-3),
            "6M" => today.AddMonths(-6),
            "1Y" => today.AddYears(-1),
            _ => today.AddMonths(-3)
        };
        _dateEnd = today;
    }
}