using FinanceManager.Components.Components.Features.FinancialAccounts.Shared;
using FinanceManager.Components.Helpers;
using FinanceManager.Components.HttpClients;
using FinanceManager.Components.Services;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.FinancialAccounts.Investments.Dtos;
using FinanceManager.Domain.FinancialAccounts.Investments.Entities;
using FinanceManager.Domain.Identity.Services;
using FinanceManager.Domain.MoneyFlow.Entities;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using MudBlazor;

namespace FinanceManager.Components.Components.Features.FinancialAccounts.InvestmentAccountComponents;

public partial class InvestmentAccountDetailsPageContent : ComponentBase, IAsyncDisposable
{
    [Parameter] public required int AccountId { get; set; }

    [Inject] public required InvestmentTransactionHttpClient TransactionHttpClient { get; set; }
    [Inject] public required InvestmentValuationHttpClient ValuationHttpClient { get; set; }
    [Inject] public required AssetsHttpClient AssetsHttpClient { get; set; }
    [Inject] public required InvestmentAccountHttpClient InvestmentAccountHttpClient { get; set; }
    [Inject] public required ILoginService LoginService { get; set; }
    [Inject] public required ISettingsService SettingsService { get; set; }
    [Inject] public required ISnackbar Snackbar { get; set; }
    [Inject] public required IBrowserViewportService BrowserViewportService { get; set; }
    [Inject] public required ILogger<InvestmentAccountDetailsPageContent> Logger { get; set; }

    private readonly Guid _viewportSubscriptionId = Guid.NewGuid();
    private bool _isMobile;
    private bool _insightsDrawerOpen;
    private bool _isLoading = true;
    private int? _loadedAccountId;
    private string _accountName = "Investments";
    private readonly string _accountTypeLabel = "Investment account";
    private Currency _currency = DefaultCurrency.USD;
    private List<InvestmentTransactionDto> _transactions = [];
    private List<HoldingRow> _holdings = [];

    // Range / chart state.
    private string _selectedRange = "3M";
    private DateTime _dateStart;
    private DateTime _dateEnd = DateTime.UtcNow;
    private DateRange? _customDateRange;
    private bool _isChartLoading;
    private int _chartRefreshVersion;
    private decimal _currentBalance;
    private decimal _balanceChange;
    private decimal? _balanceChangePercent;
    public List<TimeSeriesModel> ChartData { get; set; } = [];

    // Toolbar filter state.
    private string? _searchText;
    private AccountHistoryToolbar.TxFilter? _activeFilter;
    private List<InvestmentTransactionDto>? _top5;
    private List<InvestmentTransactionDto>? _bottom5;

    // Add/edit overlay state.
    private bool _formVisible;
    private InvestmentTransactionDto? _editingTransaction;

    protected override async Task OnParametersSetAsync()
    {
        if (_loadedAccountId == AccountId) return;
        SetDateRangeForSelection();
        await LoadAsync(initialLoad: true);
    }

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
        GC.SuppressFinalize(this);
    }

    private void ToggleInsightsDrawer() => _insightsDrawerOpen = !_insightsDrawerOpen;

    private void CloseInsightsDrawer() => _insightsDrawerOpen = false;

    private async Task LoadAsync(bool initialLoad = false)
    {
        _isLoading = true;
        _currency = await SettingsService.GetCurrencyAsync();
        try
        {
            _accountName = (await InvestmentAccountHttpClient.GetAccountAsync(AccountId))?.Name ?? "Investments";
            _transactions = [.. await TransactionHttpClient.GetByAccountAsync(AccountId)];
            _loadedAccountId = AccountId;

            if (initialLoad)
                ApplyAutomaticCustomRange();

            await UpdateInfo();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to load investment account {AccountId}", AccountId);
            Snackbar.Add("Could not load the investment account.", Severity.Error);
        }
        finally
        {
            _isLoading = false;
        }
    }

    // Recomputes the in-range movers and, unless suppressed, queues a fresh chart + holdings refresh.
    private Task UpdateInfo(bool refreshChart = true)
    {
        _currency = SettingsService.GetCurrency();

        var filtered = GetFilteredTransactions();
        var ordered = filtered.OrderByDescending(CashImpact).ToList();
        _top5 = [.. ordered.Where(t => CashImpact(t) > 0).Take(5)];
        _bottom5 = [.. ordered.Where(t => CashImpact(t) < 0).OrderBy(CashImpact).Take(5)];

        if (refreshChart)
            QueueChartDataRefresh();

        return Task.CompletedTask;
    }

    private void QueueChartDataRefresh()
    {
        var refreshVersion = ++_chartRefreshVersion;
        // Snapshot the request inputs so an in-flight refresh keeps using the account/range it
        // was started with, even if a newer range/account is selected before it completes.
        var accountId = AccountId;
        var currency = _currency;
        var dateStart = _dateStart;
        var dateEnd = _dateEnd;
        _isChartLoading = true;
        ChartData.Clear();

        _ = InvokeAsync(async () =>
        {
            try
            {
                await UpdateChartData(refreshVersion, accountId, currency, dateStart, dateEnd);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error while loading investment account chart data for account ID {AccountId}", AccountId);
            }
            finally
            {
                if (refreshVersion == _chartRefreshVersion)
                    _isChartLoading = false;

                StateHasChanged();
            }
        });
    }

    private async Task UpdateChartData(int refreshVersion, int accountId, Currency currency, DateTime dateStart, DateTime dateEnd)
    {
        var series = await ValuationHttpClient.GetValueSeriesAsync(accountId, currency.Id, dateStart, dateEnd);
        var holdings = await ValuationHttpClient.GetHoldingsAsync(accountId, dateEnd);
        var user = await LoginService.GetLoggedUser();
        var appreciation = user is null
            ? null
            : (await AssetsHttpClient.GetUnrealizedGainLossPerAccount(user.UserId, currency, dateEnd))
                .SingleOrDefault(x => x.AccountId == accountId);
        if (refreshVersion != _chartRefreshVersion || accountId != AccountId) return;

        // Keep the full ordered series for balance maths; trim only leading zeros for the chart so
        // a range that starts before the first holding still reports the true change from zero.
        var orderedSeries = series
            .OrderBy(kv => kv.Key)
            .Select(kv => new TimeSeriesModel(kv.Key, kv.Value))
            .ToList();

        ChartData.Clear();
        ChartData.AddRange(orderedSeries.SkipWhile(x => x.Value == 0));
        UpdateBalanceFromChartData(orderedSeries, appreciation);
        UpdateHoldings(holdings, dateEnd);
    }

    private void UpdateBalanceFromChartData(
        IReadOnlyList<TimeSeriesModel> balanceSeries,
        UnrealizedGainLossAccountResult? appreciation)
    {
        _currentBalance = balanceSeries.LastOrDefault()?.Value ?? 0;
        _balanceChange = appreciation?.UnrealizedGainLoss ?? 0m;
        _balanceChangePercent = appreciation is null ? null : appreciation.UnrealizedGainLossPercent;
    }

    private void UpdateHoldings(IReadOnlyDictionary<long, decimal> holdings, DateTime asOf)
    {
        // Value each holding from its latest trade on or before the as-of date so historical
        // ranges don't pull ticker/price metadata from trades that happen after the range end.
        var asOfDate = DateOnly.FromDateTime(asOf);
        var latestByListing = _transactions
            .Where(t => t.TradeDate <= asOfDate)
            .GroupBy(t => t.AssetListingId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(t => t.TradeDate).ThenByDescending(t => t.Id).First());

        var rows = new List<HoldingRow>();
        foreach (var (listingId, quantity) in holdings)
        {
            if (quantity == 0m || !latestByListing.TryGetValue(listingId, out var latest)) continue;
            rows.Add(new HoldingRow(listingId, latest.Ticker, latest.ExchangeName, latest.Currency, quantity, latest.UnitPrice, quantity * latest.UnitPrice));
        }
        _holdings = [.. rows.OrderByDescending(h => h.Value)];
    }

    private bool HasActiveFilter => !string.IsNullOrWhiteSpace(_searchText) || _activeFilter is not null;

    private List<InvestmentTransactionDto> GetFilteredTransactions()
    {
        IEnumerable<InvestmentTransactionDto> transactions = _transactions
            .Where(t => t.TradeDate >= DateOnly.FromDateTime(_dateStart) && t.TradeDate <= DateOnly.FromDateTime(_dateEnd));

        // Income/Expense maps to the cash direction of the trade: a sell brings cash in, a buy takes it out.
        if (_activeFilter == AccountHistoryToolbar.TxFilter.Income)
            transactions = transactions.Where(t => t.Type == InvestmentTransactionType.Sell);
        else if (_activeFilter == AccountHistoryToolbar.TxFilter.Expense)
            transactions = transactions.Where(t => t.Type == InvestmentTransactionType.Buy);

        if (!string.IsNullOrWhiteSpace(_searchText))
        {
            var needle = _searchText.Trim();
            transactions = transactions.Where(t =>
                t.Ticker.Contains(needle, StringComparison.OrdinalIgnoreCase)
                || t.ExchangeName.Contains(needle, StringComparison.OrdinalIgnoreCase)
                || (t.Notes is not null && t.Notes.Contains(needle, StringComparison.OrdinalIgnoreCase)));
        }

        return [.. transactions
            .OrderByDescending(t => t.TradeDate)
            .ThenByDescending(t => t.Id)];
    }

    private async Task OnRangeChanged(string value)
    {
        _selectedRange = value;
        SetDateRangeForSelection();
        await UpdateInfo();
        StateHasChanged();
    }

    private async Task OnCustomDateRangeChanged(DateRange? range)
    {
        _customDateRange = range;
        _selectedRange = AccountDetailsHero.CustomRangeKey;
        SetDateRangeForSelection();
        await UpdateInfo();
        StateHasChanged();
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

    private void SetDateRangeForSelection()
    {
        var today = DateTime.UtcNow;
        (_dateStart, _dateEnd) = DateRangeHelper.GetAccountDetailsRange(
            _selectedRange, _customDateRange?.Start, _customDateRange?.End,
            today.AddMonths(-3), today.AddMonths(-3), today);
    }

    // On the first load, widen the range to the oldest recorded trade when it predates the
    // default window, so navigating in doesn't hide existing history behind an empty range.
    private void ApplyAutomaticCustomRange()
    {
        var oldestTradeDate = _transactions.MinBy(t => t.TradeDate)?.TradeDate.ToDateTime(TimeOnly.MinValue);
        var expandedStart = DateRangeHelper.GetExpandedStart(_dateStart, oldestTradeDate);
        if (expandedStart is not DateTime oldestStart) return;

        _selectedRange = AccountDetailsHero.CustomRangeKey;
        _dateStart = oldestStart;
        _customDateRange = new DateRange(_dateStart, _dateEnd);
    }

    private static decimal CashImpact(InvestmentTransactionDto t)
    {
        var gross = t.Quantity * t.UnitPrice;
        var fee = t.Fee ?? 0m;
        return t.Type == InvestmentTransactionType.Sell ? gross - fee : -(gross + fee);
    }

    private void ShowAdd()
    {
        _editingTransaction = null;
        _formVisible = true;
    }

    private void ShowEdit(InvestmentTransactionDto tx)
    {
        _editingTransaction = tx;
        _formVisible = true;
    }

    private Task OnFormVisibleChanged(bool visible)
    {
        _formVisible = visible;
        return Task.CompletedTask;
    }

    private async Task OnTransactionSavedAsync()
    {
        _formVisible = false;
        await LoadAsync();
    }

    private async Task DeleteAsync(InvestmentTransactionDto tx)
    {
        try
        {
            if (await TransactionHttpClient.DeleteAsync(AccountId, tx.Id))
            {
                Snackbar.Add("Transaction removed.", Severity.Success);
                await LoadAsync();
            }
            else
            {
                Snackbar.Add("Could not remove the transaction.", Severity.Error);
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to delete investment transaction {TransactionId} for account {AccountId}", tx.Id, AccountId);
            Snackbar.Add("Could not remove the transaction.", Severity.Error);
        }
    }

    private sealed record HoldingRow(long ListingId, string Ticker, string ExchangeName, string Currency, decimal Quantity, decimal LatestPrice, decimal Value);
}