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
using FinanceManager.Domain.Identity.Entities;
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
    [Inject] public required InvestmentAccountDetailsSnapshotStore DetailsSnapshotStore { get; set; }
    [Inject] public required MoneyFlowHttpClient MoneyFlowHttpClient { get; set; }

    private const string _defaultAccountName = "Investments";
    private const string _defaultBenchmarkName = "Polish inflation";

    private readonly Guid _viewportSubscriptionId = Guid.NewGuid();
    private bool _isMobile;
    private bool _insightsDrawerOpen;
    private bool _isLoading = true;
    private int? _loadedAccountId;
    private UserSession? _user;
    private string _accountName = _defaultAccountName;
    private readonly string _accountTypeLabel = "Investment account";
    private Currency _currency = DefaultCurrency.USD;
    private List<InvestmentTransactionDto> _transactions = [];
    private IReadOnlyDictionary<long, InvestmentTransactionValuationDto> _valuations =
        new Dictionary<long, InvestmentTransactionValuationDto>();
    private List<InvestmentHoldingModel> _holdings = [];

    // Keeps a slower load from overwriting content a newer load already committed.
    private readonly RefreshVersionGate _detailsGate = new();

    // Last content applied to the screen. Kept whole so a valuations request that fails can fall
    // back to the exact list already painted, rather than a re-ordered rebuild that would read as
    // a change and trigger a needless repaint and re-write.
    private InvestmentAccountDetailsModel? _applied;

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
    public List<TimeSeriesModel> CapitalData { get; set; } = [];
    private string _benchmarkName = _defaultBenchmarkName;

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

    // Stale-while-revalidate: paint the last-rendered trades and their valuations instantly, always
    // re-fetch, and only repaint and re-persist when the rendered content changed. Chart data,
    // holdings and the appreciation figures have their own per-range snapshot, queued by UpdateInfo.
    // See docs/codebase/UI-SNAPSHOTS.md.
    private async Task LoadAsync(bool initialLoad = false)
    {
        _user ??= await LoginService.GetLoggedUser();
        if (_user is null)
        {
            _isLoading = false;
            return;
        }

        var accountId = AccountId;
        _loadedAccountId = accountId;
        var detailsVersion = _detailsGate.Claim();
        _chartGate.Claim();
        var snapshotPainted = false;
        var coreDetailsApplied = false;

        var result = await DetailsSnapshotStore.RefreshAsync(
            _user.UserId,
            accountId,
            _detailsGate,
            fetchAsync: () => FetchDetailsAsync(
                accountId,
                detailsVersion,
                async model =>
                {
                    if (!_detailsGate.IsCurrent(detailsVersion)) return;

                    // A chart refresh already started from a snapshot is still valid when the
                    // fresh account has the same chart inputs. Otherwise the fresh trades must
                    // supersede it, even though their valuation enrichment is not ready yet.
                    var refreshChart = !snapshotPainted || !HasSameChartInputs(model);
                    coreDetailsApplied = true;
                    await ApplyDetails(model, initialLoad, refreshChart);
                }),

            // A reload triggered by the user's own edit must not repaint the stored snapshot: it
            // still holds the pre-edit trades and would flash the change back out for a moment.
            onSnapshotPainted: initialLoad
                ? model =>
                {
                    snapshotPainted = true;
                    return ApplyDetails(model, expandRange: true);
                }
        : null,
            onRefreshed: model => ApplyDetails(
                model,
                expandRange: initialLoad,
                refreshChart: !coreDetailsApplied),
            claimedVersion: detailsVersion);

        // A failed refresh behind painted content leaves it on screen; only a page with nothing to
        // show reports the failure the user can act on.
        if (result.IsBlockingFailure)
        {
            Logger.LogError(result.Error, "Failed to load investment account {AccountId}", AccountId);
            Snackbar.Add("Could not load the investment account.", Severity.Error);
        }

        _isLoading = false;
        StateHasChanged();
    }

    // Account metadata, trades and valuation enrichment are independent requests. Paint the account
    // and trades as soon as the two core requests finish; valuation enrichment continues in parallel
    // and updates the same rows when it arrives, so chart loading does not inherit its latency.
    private async Task<InvestmentAccountDetailsModel?> FetchDetailsAsync(
        int accountId,
        int version,
        Func<InvestmentAccountDetailsModel, Task> onCoreDetailsReady)
    {
        if (_user is null) return null;

        // Pinned before the first await so every request, and the model they produce, belong to the
        // same account even if the parameter moves under a rapid navigation between two accounts.
        var userId = _user.UserId;
        var currencyTask = SettingsService.GetCurrencyAsync();

        var accountTask = InvestmentAccountHttpClient.GetAccountAsync(accountId);
        var transactionsTask = TransactionHttpClient.GetByAccountAsync(accountId);
        var currency = await currencyTask;
        var valuationsTask = FetchValuationsAsync(accountId, currency);

        await Task.WhenAll(accountTask, transactionsTask);

        var account = await accountTask;
        var transactions = (await transactionsTask).ToList();
        IReadOnlyList<InvestmentTransactionValuationDto> retainedValuations = _applied is { } applied
            && applied.AccountId == accountId
            && applied.Currency.Id == currency.Id
            ? applied.Valuations
            : [];

        if (_detailsGate.IsCurrent(version))
        {
            await onCoreDetailsReady(new InvestmentAccountDetailsModel(
                userId,
                accountId,
                account?.Name ?? _defaultAccountName,
                currency,
                transactions,
                [.. retainedValuations]));
        }

        var valuations = await valuationsTask;

        return new InvestmentAccountDetailsModel(
            userId,
            accountId,
            account?.Name ?? _defaultAccountName,
            currency,
            transactions,
            [.. valuations]);
    }

    // Per-transaction purchase value / current valuation / gain-loss is priced server-side (needs
    // market prices + FX). It only enriches the trade rows, so a failure here must not hide trades
    // that loaded fine — the rows fall back to their cash impact, which is what they showed before
    // pricing existed at all. Reporting the failure instead would replace a working trade list with
    // "No transactions yet", which is the one thing that would be untrue.
    private async Task<IReadOnlyList<InvestmentTransactionValuationDto>> FetchValuationsAsync(int accountId, Currency currency)
    {
        try
        {
            return await ValuationHttpClient.GetTransactionValuationsAsync(accountId, currency.Id);
        }
        catch (Exception ex)
        {
            // Logged through the component's own parameter rather than the pinned local: an account
            // id reaching a log sink through a local trips CodeQL's cleartext-storage query, while
            // the property form every other {AccountId} log in this repo uses does not. Same value
            // outside a mid-flight parameter change, and this is a diagnostic warning either way.
            Logger.LogWarning(ex, "Failed to load transaction valuations for account {AccountId}", AccountId);

            // Carry over what is already on screen so a blip does not strip priced rows — but only
            // while it was priced in the currency being rendered now. Amounts from a preference the
            // user has since changed would be attached to a model labelled with the new one.
            return _applied is { } applied && applied.AccountId == accountId && applied.Currency.Id == currency.Id
                ? applied.Valuations
                : [];
        }
    }

    // Renders one version of the trade list — a painted snapshot or a fresh response — and queues
    // the chart, holdings and appreciation figures that go with it.
    private Task ApplyDetails(InvestmentAccountDetailsModel model, bool expandRange, bool refreshChart = true)
    {
        _applied = model;
        _accountName = model.Name;
        _currency = model.Currency;
        _transactions = [.. model.Transactions];
        _valuations = model.Valuations.ToDictionary(v => v.TransactionId);
        _isLoading = false;

        // Only widen the window on an account's first load: the trades themselves say how far back
        // its history reaches, and a later reload must not move a range the user has since picked.
        if (expandRange)
            ApplyAutomaticCustomRange();

        UpdateInfo(refreshChart);
        return InvokeAsync(StateHasChanged);
    }

    private bool HasSameChartInputs(InvestmentAccountDetailsModel model) =>
        _currency.Id == model.Currency.Id && _transactions.SequenceEqual(model.Transactions);

    // Recomputes the in-range movers and, unless suppressed, queues a fresh chart + holdings refresh.
    private void UpdateInfo(bool refreshChart = true)
    {
        var filtered = GetFilteredTransactions();
        var ordered = filtered.OrderByDescending(CashImpact).ToList();
        _top5 = [.. ordered.Where(t => CashImpact(t) > 0).Take(5)];
        _bottom5 = [.. ordered.Where(t => CashImpact(t) < 0).OrderBy(CashImpact).Take(5)];

        if (refreshChart)
            QueueChartDataRefresh();
    }

    private void QueueChartDataRefresh()
    {
        if (_user is null) return;

        var version = _chartGate.Claim();
        var userId = _user.UserId;
        var accountId = AccountId;
        var currency = _currency;
        var selectedRange = _selectedRange;
        var dateStart = _dateStart;
        var dateEnd = _dateEnd;
        var transactions = _transactions.ToList();
        _isChartLoading = true;

        _ = InvokeAsync(async () =>
        {
            try
            {
                // Resolved here rather than on the load path: the benchmark only feeds the chart, so
                // the trade list must not wait for the lookup that resolves it.
                var benchmark = await UserSettingsService.GetBenchmarkAsync();
                var benchmarkName = benchmark?.Ticker ?? _defaultBenchmarkName;

                var result = await ChartSnapshotStore.RefreshInvestmentAsync(
                    userId,
                    accountId,
                    currency.Id,
                    benchmark?.ListingId,
                    AccountChartSnapshotStore.BuildRangeKey(selectedRange, dateStart, dateEnd),
                    _chartGate,
                    version,
                    () => FetchChartModel(
                        userId,
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
        // These requests use separate API scopes and have no data dependency on one another. Keep
        // them on the same critical path only at the join so one slow valuation cannot serialize the
        // other chart inputs or delay the first render unnecessarily.
        var chartRequests = await InvestmentChartRequestLoader.LoadAsync(
            () => ValuationHttpClient.GetValueSeriesAsync(accountId, currency.Id, dateStart, dateEnd),
            () => ValuationHttpClient.GetHoldingsAsync(accountId, dateEnd),
            async () => await AssetsHttpClient.GetUnrealizedGainLossPerAccount(userId, currency, dateEnd),
            () => ValuationHttpClient.GetBenchmarkSeriesAsync(
                accountId,
                currency.Id,
                dateStart,
                dateEnd,
                benchmark?.ListingId),
            async () => await MoneyFlowHttpClient.GetCapital(userId, currency, dateStart, dateEnd, [accountId]));

        var series = chartRequests.Series;
        var holdings = chartRequests.Holdings;
        var appreciation = chartRequests.Appreciation.SingleOrDefault(x => x.AccountId == accountId);

        // Keep the full ordered series for balance maths; trim only leading zeros for the chart so
        // a range that starts before the first holding still reports the true change from zero.
        var orderedSeries = series
            .OrderBy(kv => kv.Key)
            .Select(kv => new TimeSeriesModel(kv.Key, kv.Value))
            .ToList();
        // The benchmark tracks the account's own contributions, so it is asked for the whole range
        // and starts itself on the day the account first holds something — no base point to seed.
        var benchmarkSeries = chartRequests.BenchmarkSeries;
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
            BuildHoldings(transactions, holdings, dateEnd),
            [.. chartRequests.CapitalSeries]);
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
        CapitalData.Clear();
        CapitalData.AddRange(model.CapitalSeries ?? []);
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
        CapitalData.Clear();
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
                quantity * latest.UnitPrice,
                latest.AssetType));
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

    private void OnRangeChanged(string value)
    {
        _selectedRange = value;
        SetDateRangeForSelection();
        UpdateInfo();
        StateHasChanged();
    }

    private void OnCustomDateRangeChanged(DateRange? range)
    {
        _customDateRange = range;
        _selectedRange = AccountHistoryToolbar.CustomRangeKey;
        SetDateRangeForSelection();
        UpdateInfo();
        StateHasChanged();
    }

    private void OnSearchChanged(string? value)
    {
        _searchText = value;
        UpdateInfo(refreshChart: false);
        StateHasChanged();
    }

    private void OnTxFilterChanged(AccountHistoryToolbar.TxFilter? value)
    {
        _activeFilter = value;
        UpdateInfo(refreshChart: false);
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

        _selectedRange = AccountHistoryToolbar.CustomRangeKey;
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