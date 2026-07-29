using FinanceManager.Components.Features.FinancialAccounts.Components.Shared;
using FinanceManager.Components.Features.FinancialAccounts.HttpClients;
using FinanceManager.Components.Features.FinancialAccounts.Models;
using FinanceManager.Components.Features.FinancialAccounts.Services;
using FinanceManager.Components.Features.Identity.Services;
using FinanceManager.Components.Features.MoneyFlow.HttpClients;
using FinanceManager.Components.Shared.Helpers;
using FinanceManager.Components.Shared.Services;
using FinanceManager.Domain.Assets.Dtos;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.FinancialAccounts.Investments.Dtos;
using FinanceManager.Domain.FinancialAccounts.Investments.Entities;
using FinanceManager.Domain.Identity.Services;
using FinanceManager.Domain.MoneyFlow.Entities;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using MudBlazor;

namespace FinanceManager.Components.Features.FinancialAccounts.Components.InvestmentAccountComponents;

public partial class InvestmentAccountDetailsPageContent : ComponentBase, IAsyncDisposable
{
    [Parameter] public required int AccountId { get; set; }

    [Inject] public required InvestmentTransactionHttpClient TransactionHttpClient { get; set; }
    [Inject] public required InvestmentValuationHttpClient ValuationHttpClient { get; set; }
    [Inject] public required AssetsHttpClient AssetsHttpClient { get; set; }
    [Inject] public required InvestmentAccountHttpClient InvestmentAccountHttpClient { get; set; }
    [Inject] public required ILoginService LoginService { get; set; }
    [Inject] public required ISettingsService SettingsService { get; set; }
    [Inject] public required UserSettingsService UserSettingsService { get; set; }
    [Inject] public required ISnackbar Snackbar { get; set; }
    [Inject] public required IBrowserViewportService BrowserViewportService { get; set; }
    [Inject] public required ILogger<InvestmentAccountDetailsPageContent> Logger { get; set; }
    [Inject] public required AccountChartSnapshotStore ChartSnapshotStore { get; set; }

    private readonly Guid _viewportSubscriptionId = Guid.NewGuid();
    private bool _isMobile;
    private bool _insightsDrawerOpen;
    private bool _isLoading = true;
    private int? _loadedAccountId;
    private string _accountName = "Investments";
    private readonly string _accountTypeLabel = "Investment account";
    private Currency _currency = DefaultCurrency.USD;
    private List<InvestmentTransactionDto> _transactions = [];
    private IReadOnlyDictionary<long, InvestmentTransactionValuationDto> _valuations =
        new Dictionary<long, InvestmentTransactionValuationDto>();
    private List<InvestmentHoldingModel> _holdings = [];

    // Range / chart state.
    private string _selectedRange = "3M";
    private DateTime _dateStart;
    private DateTime _dateEnd = DateTime.UtcNow;
    private DateRange? _customDateRange;
    private bool _isChartLoading;
    private readonly RefreshVersionGate _chartGate = new();
    private decimal _currentBalance;
    private decimal _capitalValue;
    private decimal _currentValue;
    private decimal _balanceChange;
    private decimal? _balanceChangePercent;
    public List<TimeSeriesModel> ChartData { get; set; } = [];
    public List<TimeSeriesModel> BenchmarkData { get; set; } = [];
    private InstrumentSearchResultDto? _benchmark;
    private string _benchmarkName = "Polish inflation";

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
        _benchmark = await UserSettingsService.GetBenchmarkAsync();
        _benchmarkName = _benchmark?.Ticker ?? "Polish inflation";
        try
        {
            _accountName = (await InvestmentAccountHttpClient.GetAccountAsync(AccountId))?.Name ?? "Investments";
            _transactions = [.. await TransactionHttpClient.GetByAccountAsync(AccountId)];
            _loadedAccountId = AccountId;

            if (initialLoad)
                ApplyAutomaticCustomRange();

            await LoadValuationsAsync();
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

    // Per-transaction purchase value / current valuation / gain-loss is priced server-side (needs
    // market prices + FX). It only enriches the detail rows, so a failure here must not hide the
    // transaction list itself.
    private async Task LoadValuationsAsync()
    {
        try
        {
            _valuations = (await ValuationHttpClient.GetTransactionValuationsAsync(AccountId, _currency.Id))
                .ToDictionary(v => v.TransactionId);
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Failed to load transaction valuations for account {AccountId}", AccountId);
            _valuations = new Dictionary<long, InvestmentTransactionValuationDto>();
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
        var version = _chartGate.Claim();
        var accountId = AccountId;
        var currency = _currency;
        var selectedRange = _selectedRange;
        var dateStart = _dateStart;
        var dateEnd = _dateEnd;
        var transactions = _transactions.ToList();
        var benchmark = _benchmark;
        var benchmarkName = _benchmarkName;
        _isChartLoading = true;

        _ = InvokeAsync(async () =>
        {
            try
            {
                var user = await LoginService.GetLoggedUser();
                if (user is null) return;

                var result = await ChartSnapshotStore.RefreshInvestmentAsync(
                    user.UserId,
                    accountId,
                    currency.Id,
                    benchmark?.ListingId,
                    AccountChartSnapshotStore.BuildRangeKey(selectedRange, dateStart, dateEnd),
                    _chartGate,
                    version,
                    () => FetchChartModel(
                        user.UserId,
                        accountId,
                        currency,
                        selectedRange,
                        dateStart,
                        dateEnd,
                        transactions,
                        benchmark,
                        benchmarkName),
                    onSnapshotPainted: ApplyChartModel,
                    onSnapshotMissing: ShowChartLoading,
                    onRefreshed: ApplyChartModel);

                if (_chartGate.IsCurrent(version) && result.IsBlockingFailure)
                    Logger.LogError(result.Error, "Error while loading investment account chart data.");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error while loading investment account chart data.");
            }
            finally
            {
                if (_chartGate.IsCurrent(version))
                    _isChartLoading = false;

                StateHasChanged();
            }
        });
    }

    private async Task<InvestmentAccountChartModel?> FetchChartModel(
        int userId,
        int accountId,
        Currency currency,
        string selectedRange,
        DateTime dateStart,
        DateTime dateEnd,
        List<InvestmentTransactionDto> transactions,
        InstrumentSearchResultDto? benchmark,
        string benchmarkName)
    {
        var series = await ValuationHttpClient.GetValueSeriesAsync(accountId, currency.Id, dateStart, dateEnd);
        var holdings = await ValuationHttpClient.GetHoldingsAsync(accountId, dateEnd);
        var appreciation = (await AssetsHttpClient.GetUnrealizedGainLossPerAccount(userId, currency, dateEnd))
            .SingleOrDefault(x => x.AccountId == accountId);

        // Keep the full ordered series for balance maths; trim only leading zeros for the chart so
        // a range that starts before the first holding still reports the true change from zero.
        var orderedSeries = series
            .OrderBy(kv => kv.Key)
            .Select(kv => new TimeSeriesModel(kv.Key, kv.Value))
            .ToList();
        var basePoint = orderedSeries.FirstOrDefault(x => x.Value > 0);
        var benchmarkSeries = basePoint is not null
            ? await ValuationHttpClient.GetBenchmarkSeriesAsync(
                currency.Id,
                basePoint.DateTime,
                dateEnd,
                basePoint.Value,
                benchmark?.ListingId)
            : new Dictionary<DateTime, decimal>();
        var currentBalance = orderedSeries.LastOrDefault()?.Value ?? 0;

        // Capital value (remaining buy cost) and current valuation are the two source-of-truth
        // figures; the gain/loss shown in the hero and the breakdown card is derived purely from
        // their difference so the number can never disagree with the two amounts on display.
        var capitalValue = appreciation?.CostBasis ?? 0m;
        var currentValue = appreciation?.CurrentValue ?? 0m;
        var balanceChange = currentValue - capitalValue;
        return new InvestmentAccountChartModel(
            selectedRange,
            dateStart,
            dateEnd,
            [.. orderedSeries.SkipWhile(x => x.Value == 0)],
            [.. benchmarkSeries.OrderBy(x => x.Key).Select(x => new TimeSeriesModel(x.Key, x.Value))],
            benchmarkName,
            currentBalance,
            capitalValue,
            currentValue,
            balanceChange,
            capitalValue == 0m ? null : balanceChange / capitalValue * 100m,
            BuildHoldings(transactions, holdings, dateEnd));
    }

    // Only chart data is restored. The selected range and its dates stay owned by the user's
    // selection: the snapshot key already pins a snapshot to the range it was captured for, and
    // writing those fields back here would revert the range chip mid-refresh and shift the date
    // window that a concurrent entries load is reading.
    private Task ApplyChartModel(InvestmentAccountChartModel model)
    {
        ChartData.Clear();
        ChartData.AddRange(model.Series);
        BenchmarkData.Clear();
        BenchmarkData.AddRange(model.BenchmarkSeries);
        _benchmarkName = model.BenchmarkName;
        _currentBalance = model.CurrentBalance;
        _capitalValue = model.CapitalValue;
        _currentValue = model.CurrentValue;
        _balanceChange = model.BalanceChange;
        _balanceChangePercent = model.BalanceChangePercent;
        _holdings = model.Holdings;
        _isChartLoading = false;
        return InvokeAsync(StateHasChanged);
    }

    private Task ShowChartLoading()
    {
        ChartData.Clear();
        BenchmarkData.Clear();
        _isChartLoading = true;
        return InvokeAsync(StateHasChanged);
    }

    private static List<InvestmentHoldingModel> BuildHoldings(
        List<InvestmentTransactionDto> transactions,
        IReadOnlyDictionary<long, decimal> holdings,
        DateTime asOf)
    {
        // Value each holding from its latest trade on or before the as-of date so historical
        // ranges don't pull ticker/price metadata from trades that happen after the range end.
        var asOfDate = DateOnly.FromDateTime(asOf);
        var latestByListing = transactions
            .Where(t => t.TradeDate <= asOfDate)
            .GroupBy(t => t.AssetListingId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(t => t.TradeDate).ThenByDescending(t => t.Id).First());

        var rows = new List<InvestmentHoldingModel>();
        foreach (var (listingId, quantity) in holdings)
        {
            if (quantity == 0m || !latestByListing.TryGetValue(listingId, out var latest)) continue;
            rows.Add(new InvestmentHoldingModel(
                listingId,
                latest.Ticker,
                latest.ExchangeName,
                latest.Currency,
                quantity,
                latest.UnitPrice,
                quantity * latest.UnitPrice));
        }
        return [.. rows.OrderByDescending(h => h.Value)];
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
}