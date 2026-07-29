using FinanceManager.Application.Identity.Users;
using FinanceManager.Components.Features.FinancialAccounts.Components.Shared;
using FinanceManager.Components.Features.FinancialAccounts.HttpClients;
using FinanceManager.Components.Features.FinancialAccounts.Models;
using FinanceManager.Components.Features.FinancialAccounts.Services;
using FinanceManager.Components.Features.Identity.Services;
using FinanceManager.Components.Features.MoneyFlow.HttpClients;
using FinanceManager.Components.Shared.Helpers;
using FinanceManager.Components.Shared.Services;
using FinanceManager.Domain.FinancialAccounts.Bond.Entities;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.FinancialAccounts.Shared.Services;
using FinanceManager.Domain.Identity.Entities;
using FinanceManager.Domain.Identity.Services;
using FinanceManager.Domain.MoneyFlow.Entities;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using MudBlazor;

namespace FinanceManager.Components.Features.FinancialAccounts.Components.BondAccountComponents.TransactionHistory;

public partial class BondAccountDetailsPageContent : ComponentBase, IAsyncDisposable
{
    private readonly Guid _viewportSubscriptionId = Guid.NewGuid();
    private bool _isMobile;
    private bool _insightsDrawerOpen;

    private const int _initialMinimumEntriesCount = 100;

    // How long the initial load may run before the blocking spinner replaces the empty page.
    // Only applies when there is no snapshot to paint in the meantime.
    private static readonly TimeSpan _spinnerDelay = TimeSpan.FromSeconds(2);

    // Keeps a slower initial load from overwriting state a newer load already committed.
    private readonly RefreshVersionGate _entriesGate = new();

    private string _selectedRange = "Month";
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
    private List<BondAccountEntry>? _top5;
    private List<BondAccountEntry>? _bottom5;
    private Currency _currency = DefaultCurrency.PLN;
    private readonly string _accountTypeLabel = "Bond account";
    private readonly List<BondDetails> _bondDetails = [];
    private UserSession? _user;
    private bool _isChartLoading;
    private readonly RefreshVersionGate _chartGate = new();

    public bool IsLoading = false;
    public BondAccount? Account { get; set; }
    public string ErrorMessage { get; set; } = string.Empty;
    public List<TimeSeriesModel> ChartData { get; set; } = [];

    [Parameter] public required int AccountId { get; set; }

    [Inject] public required IFinancialAccountService FinancialAccountService { get; set; }
    [Inject] public required AccountDataSynchronizationService AccountDataSynchronizationService { get; set; }
    [Inject] public required ISettingsService SettingsService { get; set; }
    [Inject] public required ILoginService LoginService { get; set; }
    [Inject] public required MoneyFlowHttpClient MoneyFlowHttpClient { get; set; }
    [Inject] public required BondDetailsHttpClient BondDetailsHttpClient { get; set; }
    [Inject] public required AccountDetailsSnapshotStore SnapshotStore { get; set; }
    [Inject] public required AccountChartSnapshotStore ChartSnapshotStore { get; set; }
    [Inject] public required ILogger<BondAccountDetailsPageContent> Logger { get; set; }
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

        _currency = await SettingsService.GetCurrencyAsync();

        if (Account.Entries.Count == 0)
        {
            _currentBalance = 0;
            _balanceChange = 0;
            _balanceChangePercent = null;
            _top5 = [];
            _bottom5 = [];
            if (refreshChart)
                QueueChartDataRefresh();

            return;
        }

        foreach (var id in Account.GetStoredBondsIds())
        {
            if (_bondDetails.Any(x => x.Id == id)) continue;

            var bond = await BondDetailsHttpClient.GetById(id);
            if (bond is not null)
                _bondDetails.Add(bond);
        }

        var filteredEntries = GetFilteredEntries();

        var entriesOrdered = filteredEntries.OrderByDescending(x => x.ValueChange).ToList();
        _top5 = entriesOrdered.Where(x => x.ValueChange > 0).Take(5).ToList();
        _bottom5 = entriesOrdered.Where(x => x.ValueChange < 0)
                                 .OrderBy(x => x.ValueChange)
                                 .Take(5)
                                 .ToList();

        if (refreshChart)
            QueueChartDataRefresh();

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
            await LoadInitialEntries();
            IsLoading = false;

            AccountDataSynchronizationService.AccountsChanged += AccountDataSynchronizationService_AccountsChanged;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error during initialization of BondAccountDetailsPageContent for account ID {AccountId}", AccountId);
        }
    }

    protected override async Task OnParametersSetAsync()
    {
        try
        {
            if (Account is not null && Account.AccountId == AccountId) return;
            IsLoading = true;
            SetDateRangeForSelection();
            await UpdateEntries(initialLoad: true);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error during initialization of BondAccountDetailsPageContent for account ID {AccountId}", AccountId);
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task UpdateEntries(bool initialLoad = false)
    {
        try
        {
            if (_user is null) return;

            var selectedStart = _dateStart;
            Account = await FetchAccount(initialLoad);

            if (initialLoad)
                ApplyAutomaticCustomRange(selectedStart);

            if (Account?.Entries is not null)
                await UpdateInfo();
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            Logger.LogError(ex, "Error while loading bond account details for account ID {AccountId}", AccountId);
        }
    }

    // Loads the account without touching rendered state, so a stale-while-revalidate run can decide
    // whether the response is worth painting.
    private async Task<BondAccount?> FetchAccount(bool initialLoad)
    {
        if (_user is null) return null;

        if (_selectedRange != AccountDetailsHero.CustomRangeKey)
            _dateEnd = DateTime.UtcNow;

        return initialLoad
            ? await FinancialAccountService.GetInitialTransactionHistory<BondAccount>(_user.UserId, AccountId, _dateStart, _dateEnd,
                _initialMinimumEntriesCount)
            : await FinancialAccountService.GetAccount<BondAccount>(_user.UserId, AccountId, _dateStart, _dateEnd);
    }

    private void QueueChartDataRefresh()
    {
        if (Account is null || _user is null) return;

        var version = _chartGate.Claim();
        var userId = _user.UserId;
        var accountId = AccountId;
        var currency = _currency;
        var selectedRange = _selectedRange;
        var dateStart = _dateStart;
        var dateEnd = _dateEnd;
        _isChartLoading = true;

        _ = InvokeAsync(async () =>
        {
            try
            {
                var result = await ChartSnapshotStore.RefreshBondAsync(
                    userId,
                    accountId,
                    currency.Id,
                    _chartGate,
                    version,
                    async () =>
                    {
                        var series = (await MoneyFlowHttpClient.GetClosingBalance(userId, currency, dateStart, dateEnd, [accountId]))
                            .SkipWhile(x => x.Value == 0)
                            .ToList();
                        var currentBalance = series.LastOrDefault()?.Value ?? 0;
                        var balanceChange = series.Count >= 2 ? series.Last().Value - series.First().Value : 0;
                        var startBalance = currentBalance - balanceChange;
                        return new AccountChartModel(
                            selectedRange,
                            dateStart,
                            dateEnd,
                            series,
                            currentBalance,
                            balanceChange,
                            startBalance == 0 ? null : balanceChange / startBalance * 100m);
                    },
                    onSnapshotPainted: ApplyChartModel,
                    onSnapshotMissing: ShowChartLoading,
                    onRefreshed: ApplyChartModel);

                if (_chartGate.IsCurrent(version) && result.IsBlockingFailure)
                    Logger.LogError(result.Error, "Error while loading bond account chart data for account ID {AccountId}", accountId);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error while loading bond account chart data for account ID {AccountId}", accountId);
            }
            finally
            {
                if (_chartGate.IsCurrent(version))
                    _isChartLoading = false;

                StateHasChanged();
            }
        });
    }

    private Task ApplyChartModel(AccountChartModel model)
    {
        _selectedRange = model.SelectedRange;
        _dateStart = model.StartDate;
        _dateEnd = model.EndDate;
        _customDateRange = model.SelectedRange == AccountDetailsHero.CustomRangeKey
            ? new DateRange(model.StartDate, model.EndDate)
            : null;
        ChartData.Clear();
        ChartData.AddRange(model.Series);
        _currentBalance = model.CurrentBalance;
        _balanceChange = model.BalanceChange;
        _balanceChangePercent = model.BalanceChangePercent;
        _isChartLoading = false;
        return InvokeAsync(StateHasChanged);
    }

    private Task ShowChartLoading()
    {
        ChartData.Clear();
        _isChartLoading = true;
        return InvokeAsync(StateHasChanged);
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
                Logger.LogError(ex, "Error while refreshing bond account details after data sync for account ID {AccountId}", AccountId);
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
        _selectedRange = AccountDetailsHero.CustomRangeKey;
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

    private List<BondAccountEntry> GetFilteredEntries()
    {
        if (Account?.Entries is null) return [];

        IEnumerable<BondAccountEntry> entries = Account.Entries;

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
                GetBondName(x).Contains(needle, StringComparison.OrdinalIgnoreCase)
                || (x.Labels is not null && x.Labels.Any(l => l.Name.Contains(needle, StringComparison.OrdinalIgnoreCase))));
        }

        return entries.OrderByDescending(x => x.PostingDate).ToList();
    }

    private string GetBondName(BondAccountEntry entry) =>
        _bondDetails.FirstOrDefault(b => b.Id == entry.BondDetailsId)?.Name ?? string.Empty;

    private void SetDateRangeForSelection()
    {
        var today = DateTime.UtcNow;
        (_dateStart, _dateEnd) = DateRangeHelper.GetAccountDetailsRange(
            _selectedRange, _customDateRange?.Start, _customDateRange?.End,
            Account?.Start ?? today.AddMonths(-3), new DateTime(today.Year, today.Month, 1, 0, 0, 0, DateTimeKind.Utc), today);
    }

    private void ApplyAutomaticCustomRange(DateTime selectedStart)
    {
        var expandedStart = DateRangeHelper.GetExpandedStart(selectedStart, Account?.Entries?.MinBy(x => x.PostingDate)?.PostingDate);
        if (expandedStart is not DateTime oldest) return;

        _selectedRange = AccountDetailsHero.CustomRangeKey;
        _dateStart = oldest;
        _customDateRange = new DateRange(_dateStart, _dateEnd);
    }

    // Stale-while-revalidate: paint the last-rendered entries instantly, always re-fetch, and only
    // repaint and re-persist when the entries actually changed. Chart data is never snapshotted —
    // UpdateInfo queues a fresh API load for it. See docs/codebase/UI-SNAPSHOTS.md.
    private async Task LoadInitialEntries()
    {
        if (_user is null) return;

        var snapshotPainted = false;

        // The fresh account is applied as served. Only its rendered content goes through the
        // snapshot model, so state the snapshot cannot carry — NextOlderEntry/NextYoungerEntry,
        // which decide whether the page shows history at all — survives the refresh.
        BondAccount? freshAccount = null;

        var result = await SnapshotStore.RefreshAsync<BondAccountEntry>(
            _user.UserId,
            AccountId,
            _entriesGate,
            fetchAsync: async () =>
            {
                freshAccount = await FetchInitialAccount(snapshotPainted);
                return freshAccount is null || _user is null
                    ? null
                    : new AccountDetailsModel<BondAccountEntry>(_user.UserId, AccountId, freshAccount.Name, freshAccount.AccountType, freshAccount.Entries);
            },
            onSnapshotPainted: model =>
            {
                snapshotPainted = true;
                return ApplyAccount(BuildAccount(model), expandRange: false);
            },
            onRefreshed: model => ApplyAccount(freshAccount ?? BuildAccount(model), expandRange: true));

        // A failed refresh behind a painted snapshot keeps the stale entries on screen instead of
        // replacing them with an error the user cannot act on.
        if (result.IsBlockingFailure && result.Error is Exception error)
            ErrorMessage = error.Message;
    }

    private async Task<BondAccount?> FetchInitialAccount(bool snapshotPainted)
    {
        var loadTask = FetchAccount(initialLoad: true);

        // With nothing painted the page is blank, so show the spinner once the load looks slow.
        if (!snapshotPainted)
        {
            var delayTask = Task.Delay(_spinnerDelay);
            if (await Task.WhenAny(loadTask, delayTask) == delayTask)
            {
                IsLoading = true;
                StateHasChanged();
            }
        }

        return await loadTask;
    }

    // Rebuilds an account from a snapshot, which stores rendered entries only.
    private static BondAccount BuildAccount(AccountDetailsModel<BondAccountEntry> model) =>
        new(model.UserId, model.AccountId, model.Name, model.Entries, model.AccountType);

    private async Task ApplyAccount(BondAccount account, bool expandRange)
    {
        Account = account;

        // Only a fresh response may widen the selected range: it is the one that knows how far
        // back the account's entries actually reach.
        if (expandRange)
            ApplyAutomaticCustomRange(_dateStart);

        await UpdateInfo();
        IsLoading = false;
        StateHasChanged();
    }
}