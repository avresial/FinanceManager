using FinanceManager.Components.Helpers;
using FinanceManager.Components.HttpClients;
using FinanceManager.Components.Services;
using FinanceManager.Domain.Entities.Currencies;
using FinanceManager.Domain.Entities.FinancialAccounts.Currencies;
using FinanceManager.Domain.Entities.MoneyFlowModels;
using FinanceManager.Domain.Entities.Users;
using FinanceManager.Domain.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using MudBlazor;

namespace FinanceManager.Components.Components.FinancialAccounts.CurrencyAccountComponents;

public partial class CurrencyAccountDetailsPageContent : ComponentBase
{
    private const int _minimumEntryCount = 50;

    private bool _isLoadingMore = false;
    private decimal _balanceChange = 100;
    private bool _loadedAllData = false;
    private DateTime _dateStart;
    private DateTime _dateEnd = DateTime.UtcNow;

    private bool _addEntryVisibility;
    private List<CurrencyAccountEntry>? _top5;
    private List<CurrencyAccountEntry>? _bottom5;
    private Currency _currency = DefaultCurrency.PLN;
    private UserSession? _user;

    private decimal? _filterFrom;
    private decimal? _filterTo;
    private DateTime? _filterDateStart;
    private DateTime? _filterDateEnd;

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

    public async Task ShowOverlay()
    {
        _addEntryVisibility = true;
        StateHasChanged();
        await Task.CompletedTask;
    }

    public async Task HideOverlay()
    {
        _addEntryVisibility = false;
        StateHasChanged();
        await Task.CompletedTask;
    }

    public async Task UpdateInfo()
    {
        if (Account is null || Account.Entries is null) return;

        UpdateLoadStateFromAccount();

        if (Account.Entries.Count == 0) return;

        var entriesOrdered = Account.Entries.OrderByDescending(x => x.ValueChange);
        _top5 = entriesOrdered.Where(x => x.ValueChange > 0).Take(5).ToList();
        _bottom5 = entriesOrdered.Skip(Math.Max(Account.Entries.Count - 5, 0))
                                .Where(x => x.ValueChange < 0)
                                .Take(5)
                                .OrderBy(x => x.ValueChange)
                                .ToList();

        _balanceChange = Account.Entries.First().Value - Account.Entries.Last().Value;

        await UpdateChartData();
    }

    public async Task LoadMore()
    {
        if (Account is null || Account.Start is null) return;
        if (_user is null) return;

        _isLoadingMore = true;

        try
        {
            var loadResult = await RecentEntriesLoader.LoadMoreAsync(
                Account,
                _dateStart,
                _dateEnd,
                CreateLoaderOptions());

            Account = loadResult.Account;
            _dateStart = loadResult.DateStart;

            await UpdateChartData();
            await UpdateInfo();
        }
        finally
        {
            _isLoadingMore = false;
        }
    }

    protected override async Task OnInitializedAsync()
    {
        try
        {
            _dateStart = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);
            _user = await LoginService.GetLoggedUser();
            if (_user is null)
            {
                IsLoading = false;
                return;
            }

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
            AccountDataSynchronizationService.AccountsChanged += AccountDataSynchronizationService_AccountsChanged;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error during initialization of CurrencyAccountDetailsPageContent for account ID {AccountId}", AccountId);
        }
    }

    protected override async Task OnParametersSetAsync()
    {
        try
        {
            if (Account is not null && Account.AccountId == AccountId) return;
            IsLoading = true;
            _loadedAllData = false;
            await UpdateEntries();
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

    private async Task UpdateEntries()
    {
        try
        {
            if (_user is null) return;

            var requestedDateStart = _dateStart;
            _dateEnd = _filterDateEnd.HasValue ? _filterDateEnd.Value.Date.AddDays(1).AddTicks(-1) : DateTime.UtcNow;
            Account = await FinancialAccountService.GetAccount<CurrencyAccount>(_user.UserId, AccountId, requestedDateStart, _dateEnd, _minimumEntryCount);
            UpdateDateStartFromLoadedEntries(requestedDateStart);

            if (Account?.Entries is not null)
                await UpdateInfo();
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            Logger.LogError(ex, "Error while loading currency account details for account ID {AccountId}", AccountId);
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

    private void UpdateLoadStateFromAccount()
    {
        if (Account is null)
        {
            _loadedAllData = false;
            return;
        }

        _loadedAllData = Account.NextOlderEntry is null;
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

    private List<CurrencyAccountEntry> GetFilteredEntries()
    {
        if (Account?.Entries is null) return [];

        IEnumerable<CurrencyAccountEntry> entries = Account.Entries;

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

    private RecentEntriesLoaderOptions<CurrencyAccount> CreateLoaderOptions() =>
        new()
        {
            LoadByDateRangeAsync = (dateStart, dateEnd) => _user is null
                ? Task.FromResult<CurrencyAccount?>(null)
                : FinancialAccountService.GetAccount<CurrencyAccount>(_user.UserId, AccountId, dateStart, dateEnd),
            GetEntryCount = account => account.Entries.Count,
            GetNextOlderReferenceDate = account => account.NextOlderEntry?.PostingDate,
            HasOlderEntries = account => account.NextOlderEntry is not null,
        };
}