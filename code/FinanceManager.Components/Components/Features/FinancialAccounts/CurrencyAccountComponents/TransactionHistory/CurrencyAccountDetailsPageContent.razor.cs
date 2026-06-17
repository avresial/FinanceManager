using FinanceManager.Application.Identity.Users;
using FinanceManager.Components.Components.Features.FinancialAccounts.Shared;
using FinanceManager.Components.Helpers;
using FinanceManager.Components.HttpClients;
using FinanceManager.Components.Services;
using FinanceManager.Domain.Entities.Currencies;
using FinanceManager.Domain.Entities.FinancialAccounts.Currencies;
using FinanceManager.Domain.Entities.MoneyFlowModels;
using FinanceManager.Domain.Enums;
using FinanceManager.Domain.Identity.Entities;
using FinanceManager.Domain.Identity.Services;
using FinanceManager.Domain.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using MudBlazor;

namespace FinanceManager.Components.Components.Features.FinancialAccounts.CurrencyAccountComponents.TransactionHistory;

public partial class CurrencyAccountDetailsPageContent : ComponentBase, IAsyncDisposable
{
    private readonly Guid _viewportSubscriptionId = Guid.NewGuid();
    private bool _isMobile;
    private bool _insightsDrawerOpen;

    private const int _initialMinimumEntriesCount = 100;

    private string _selectedRange = "Month";
    private DateTime _dateStart;
    private DateTime _dateEnd = DateTime.UtcNow;
    private DateRange? _customDateRange;

    private bool _addEntryVisibility;

    private string? _searchText;
    private AccountHistoryToolbar.TxFilter? _activeFilter;
    private HashSet<string> _selectedLabels = new(StringComparer.OrdinalIgnoreCase);
    private IEnumerable<string> _availableLabels = [];

    private decimal _currentBalance;
    private decimal _balanceChange;
    private decimal? _balanceChangePercent;
    private List<CurrencyAccountEntry>? _top5;
    private List<CurrencyAccountEntry>? _bottom5;
    private Currency _currency = DefaultCurrency.PLN;
    private string _accountTypeLabel = "Cash account";
    private UserSession? _user;
    private bool _isChartLoading;
    private int _chartRefreshVersion;

    public bool IsLoading = false;
    public CurrencyAccount? Account { get; set; }
    public string ErrorMessage { get; set; } = string.Empty;
    public List<TimeSeriesModel> ChartData { get; set; } = [];

    [Parameter] public required int AccountId { get; set; }

    [Inject] public required IFinancialAccountService FinancialAccountService { get; set; }
    [Inject] public required AccountDataSynchronizationService AccountDataSynchronizationService { get; set; }
    [Inject] public required ISettingsService SettingsService { get; set; }
    [Inject] public required ILoginService LoginService { get; set; }
    [Inject] public required MoneyFlowHttpClient MoneyFlowHttpClient { get; set; }
    [Inject] public required ILogger<CurrencyAccountDetailsPageContent> Logger { get; set; }
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

    public Task UpdateInfo(bool refreshChart = true)
    {
        if (Account is null || Account.Entries is null) return Task.CompletedTask;

        _currency = SettingsService.GetCurrency();

        if (Account.Entries.Count == 0)
        {
            _currentBalance = 0;
            _balanceChange = 0;
            _balanceChangePercent = null;
            _top5 = [];
            _bottom5 = [];
            if (refreshChart)
                QueueChartDataRefresh();

            return Task.CompletedTask;
        }

        var filteredEntries = GetFilteredEntries();

        var entriesOrdered = filteredEntries.OrderByDescending(x => x.ValueChange).ToList();
        _top5 = entriesOrdered.Where(x => x.ValueChange > 0).Take(5).ToList();
        _bottom5 = entriesOrdered.Where(x => x.ValueChange < 0)
                                 .OrderBy(x => x.ValueChange)
                                 .Take(5)
                                 .ToList();

        _currentBalance = Account.Entries.First().Value;
        _balanceChange = filteredEntries.Sum(e => e.ValueChange);

        var startBalance = _currentBalance - _balanceChange;
        _balanceChangePercent = startBalance == 0 ? null : _balanceChange / startBalance * 100m;

        _availableLabels = Account.Entries
            .SelectMany(e => e.Labels ?? [])
            .Where(l => l is not null)
            .Select(l => l.Name)
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
            .ToList();

        _accountTypeLabel = Account.AccountType switch
        {
            AccountLabel.Cash => "Cash account",
            AccountLabel.Stock => "Stock account",
            AccountLabel.Bond => "Bond account",
            AccountLabel.Crypto => "Crypto account",
            AccountLabel.Loan => "Loan account",
            AccountLabel.RealEstate => "Real estate account",
            _ => "Account"
        };

        if (refreshChart)
            QueueChartDataRefresh();

        return Task.CompletedTask;
    }

    protected override async Task OnInitializedAsync()
    {
        try
        {
            _user = await LoginService.GetLoggedUser();
            if (_user is null) return;

            SetDateRangeForSelection();

            var loadTask = UpdateEntries(initialLoad: true);
            var delayTask = Task.Delay(1000);
            var completedTask = await Task.WhenAny(loadTask, delayTask);
            if (completedTask == delayTask)
            {
                IsLoading = true;
                StateHasChanged();
                await loadTask;
            }
            AccountDataSynchronizationService.AccountsChanged += AccountDataSynchronizationService_AccountsChanged;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error during initialization of CurrencyAccountDetailsPageContent for account ID {AccountId}", AccountId);
        }
        finally
        {
            IsLoading = false;
            StateHasChanged();
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
            Logger.LogError(ex, "Error during initialization of CurrencyAccountDetailsPageContent for account ID {AccountId}", AccountId);
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

            if (_selectedRange != AccountDetailsHero.CustomRangeKey)
                _dateEnd = DateTime.UtcNow;

            var selectedStart = _dateStart;
            Account = initialLoad
                ? await FinancialAccountService.GetInitialTransactionHistory<CurrencyAccount>(_user.UserId, AccountId, _dateStart, _dateEnd,
                    _initialMinimumEntriesCount)
                : await FinancialAccountService.GetAccount<CurrencyAccount>(_user.UserId, AccountId, _dateStart, _dateEnd);

            if (initialLoad)
                ApplyAutomaticCustomRange(selectedStart);

            if (Account?.Entries is not null)
                await UpdateInfo();
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            Logger.LogError(ex, "Error while loading currency account details for account ID {AccountId}", AccountId);
        }
    }

    private void QueueChartDataRefresh()
    {
        var refreshVersion = ++_chartRefreshVersion;
        _isChartLoading = true;
        ChartData.Clear();

        _ = InvokeAsync(async () =>
        {
            try
            {
                await UpdateChartData(refreshVersion);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error while loading currency account chart data for account ID {AccountId}", AccountId);
            }
            finally
            {
                if (refreshVersion == _chartRefreshVersion)
                    _isChartLoading = false;

                StateHasChanged();
            }
        });
    }

    private async Task UpdateChartData(int refreshVersion)
    {
        if (Account is null || _user is null) return;

        _currency = SettingsService.GetCurrency();
        var chartData = await MoneyFlowHttpClient.GetClosingBalance(_user.UserId, _currency, _dateStart, _dateEnd, [AccountId]);
        if (refreshVersion != _chartRefreshVersion) return;

        ChartData.Clear();
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
                Logger.LogError(ex, "Error while refreshing currency account details after data sync for account ID {AccountId}", AccountId);
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

    private async Task OnLabelsChanged(HashSet<string> value)
    {
        _selectedLabels = new HashSet<string>(value, StringComparer.OrdinalIgnoreCase);
        await UpdateInfo(refreshChart: false);
        StateHasChanged();
    }

    private bool HasActiveFilter =>
        !string.IsNullOrWhiteSpace(_searchText) || _activeFilter.HasValue || _selectedLabels.Count > 0;

    private List<CurrencyAccountEntry> GetFilteredEntries()
    {
        if (Account?.Entries is null) return [];

        IEnumerable<CurrencyAccountEntry> entries = Account.Entries;

        if (_activeFilter == AccountHistoryToolbar.TxFilter.Income)
            entries = entries.Where(x => x.ValueChange > 0);
        else if (_activeFilter == AccountHistoryToolbar.TxFilter.Expense)
            entries = entries.Where(x => x.ValueChange < 0);

        if (_selectedLabels.Count > 0)
            entries = entries.Where(x => x.Labels is not null
                && x.Labels.Any(l => _selectedLabels.Contains(l.Name)));

        if (!string.IsNullOrWhiteSpace(_searchText))
        {
            var needle = _searchText.Trim();
            entries = entries.Where(x =>
                (!string.IsNullOrEmpty(x.Description) && x.Description.Contains(needle, StringComparison.OrdinalIgnoreCase))
                || (!string.IsNullOrEmpty(x.ContractorDetails) && x.ContractorDetails.Contains(needle, StringComparison.OrdinalIgnoreCase))
                || (x.Labels is not null && x.Labels.Any(l => l.Name.Contains(needle, StringComparison.OrdinalIgnoreCase))));
        }

        return entries.OrderByDescending(x => x.PostingDate).ToList();
    }

    private void SetDateRangeForSelection()
    {
        var today = DateTime.UtcNow;
        if (_selectedRange == AccountDetailsHero.CustomRangeKey)
        {
            _dateStart = _customDateRange?.Start ?? Account?.Start ?? today.AddMonths(-3);
            _dateEnd = _customDateRange?.End ?? today;
            if (_dateEnd > today)
                _dateEnd = today;
            return;
        }

        _dateStart = _selectedRange switch
        {
            "Month" => DateRangeHelper.GetCurrentMonthRange().Start,
            "1M" => today.AddMonths(-1),
            "3M" => today.AddMonths(-3),
            "6M" => today.AddMonths(-6),
            "YTD" => new DateTime(today.Year, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            _ => DateRangeHelper.GetCurrentMonthRange().Start,
        };
        _dateEnd = today;
    }

    private void ApplyAutomaticCustomRange(DateTime selectedStart)
    {
        var oldestFetchedEntryDate = Account?.Entries?.MinBy(x => x.PostingDate)?.PostingDate;
        if (oldestFetchedEntryDate is null || oldestFetchedEntryDate.Value >= selectedStart)
            return;

        _selectedRange = AccountDetailsHero.CustomRangeKey;
        _dateStart = oldestFetchedEntryDate.Value;
        _customDateRange = new DateRange(_dateStart, _dateEnd);
    }
}