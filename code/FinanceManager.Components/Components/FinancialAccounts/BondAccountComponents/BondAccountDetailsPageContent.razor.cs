using FinanceManager.Components.Helpers;
using FinanceManager.Components.HttpClients;
using FinanceManager.Components.Services;
using FinanceManager.Domain.Entities.Bonds;
using FinanceManager.Domain.Entities.Currencies;
using FinanceManager.Domain.Entities.MoneyFlowModels;
using FinanceManager.Domain.Entities.Users;
using FinanceManager.Domain.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;

namespace FinanceManager.Components.Components.FinancialAccounts.BondAccountComponents;

public partial class BondAccountDetailsPageContent : ComponentBase
{
    private const int _minimumEntryCount = 50;

    private bool _isLoadingMore = false;
    private decimal? _balanceChange = null;
    private UnrealizedGainLossAccountResult? _unrealizedAccount;
    private Dictionary<string, UnrealizedGainLossInstrumentResult> _unrealizedByBondId = [];
    private bool _loadedAllData = false;
    private DateTime _dateStart;
    private DateTime _dateEnd = DateTime.UtcNow;

    private bool _addEntryVisibility;

    private List<(BondAccountEntry, decimal)>? _top5;
    private List<(BondAccountEntry, decimal)>? _bottom5;
    private Currency _currency = DefaultCurrency.PLN;
    private UserSession? _user;

    private List<BondDetails> _bondDetails = [];

    private decimal? _filterFrom;
    private decimal? _filterTo;

    public bool IsLoading = false;
    public BondAccount? Account { get; set; }
    public string ErrorMessage { get; set; } = string.Empty;
    public List<TimeSeriesModel> ChartData { get; set; } = [];

    [Parameter] public required int AccountId { get; set; }

    [Inject] public required AccountDataSynchronizationService AccountDataSynchronizationService { get; set; }
    [Inject] public required IFinancialAccountService FinancialAccountService { get; set; }
    [Inject] public required BondDetailsHttpClient BondDetailsHttpClient { get; set; }
    [Inject] public required MoneyFlowHttpClient MoneyFlowHttpClient { get; set; }
    [Inject] public required AssetsHttpClient AssetsHttpClient { get; set; }
    [Inject] public required ISettingsService SettingsService { get; set; }
    [Inject] public required ILoginService LoginService { get; set; }
    [Inject] public required ILogger<BondAccountDetailsPageContent> Logger { get; set; }

    public Task ShowOverlay()
    {
        _addEntryVisibility = true;
        StateHasChanged();

        return Task.CompletedTask;
    }

    public async Task HideOverlay()
    {
        _addEntryVisibility = false;
        try
        {
            await UpdateInfo();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error while updating bond account details for account ID {AccountId}", AccountId);
            ErrorMessage = ex.Message;
        }

        StateHasChanged();
    }

    public async Task UpdateInfo()
    {
        if (Account is null || Account.Entries is null) return;

        var bondIds = Account.GetStoredBondsIds();
        foreach (var id in bondIds)
        {
            if (_bondDetails.Any(x => x.Id == id)) continue;

            var bond = await BondDetailsHttpClient.GetById(id);
            if (bond is not null)
                _bondDetails.Add(bond);
        }

        UpdateLoadStateFromAccount();
        await UpdateChartData();
        await UpdateUnrealizedGainLoss();

        if (ChartData.Count >= 2)
            _balanceChange = ChartData.Last().Value - ChartData.First().Value;

        var orderedByPrice = Account.Entries
            .OrderByDescending(x => x.ValueChange)
            .Select(x => (x, x.ValueChange))
            .ToList();

        _top5 = orderedByPrice.Take(5).ToList();
        _bottom5 = orderedByPrice.Skip(Math.Max(Account.Entries.Count - 5, 0)).Take(5).OrderBy(x => x.Item2).ToList();
    }

    public async Task LoadMore()
    {
        try
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
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error while loading more bond account details for account ID {AccountId}", AccountId);
            ErrorMessage = ex.Message;
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

            AccountDataSynchronizationService.AccountsChanged += AccountDataSynchronizationService_AccountsChanged;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error while initializing bond account details for account ID {AccountId}", AccountId);
            ErrorMessage = ex.Message;
        }
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
        try
        {
            if (_user is null) return;

            var requestedDateStart = _dateStart;
            _dateEnd = DateTime.UtcNow;
            Account = await FinancialAccountService.GetAccount<BondAccount>(_user.UserId, AccountId, requestedDateStart, _dateEnd, _minimumEntryCount);
            UpdateDateStartFromLoadedEntries(requestedDateStart);

            if (Account?.Entries is not null)
                await UpdateInfo();
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            Logger.LogError(ex, "Error while loading bond account details for account ID {AccountId}", AccountId);
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
        _unrealizedByBondId.Clear();

        if (_user is null || Account is null) return;

        var asOfDate = DateTime.UtcNow;
        var accountResults = await AssetsHttpClient.GetUnrealizedGainLossPerAccount(_user.UserId, _currency, asOfDate);
        _unrealizedAccount = accountResults.FirstOrDefault(x => x.AccountId == AccountId);

        var instrumentResults = await AssetsHttpClient.GetUnrealizedGainLossPerInstrument(_user.UserId, _currency, asOfDate);
        foreach (var result in instrumentResults.Where(x => x.AccountId == AccountId))
            _unrealizedByBondId[result.InstrumentId] = result;
    }

    private UnrealizedGainLossInstrumentResult? GetUnrealizedForBond(int bondDetailsId) =>
        _unrealizedByBondId.TryGetValue(bondDetailsId.ToString(), out var result) ? result : null;

    private async void AccountDataSynchronizationService_AccountsChanged()
    {
        try
        {
            await UpdateEntries();
            await InvokeAsync(StateHasChanged);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error while synchronizing bond account data for account ID {AccountId}", AccountId);
        }
    }

    private bool HasActiveFilter => _filterFrom.HasValue || _filterTo.HasValue;

    private List<BondAccountEntry> GetFilteredEntries()
    {
        if (Account?.Entries is null) return [];

        IEnumerable<BondAccountEntry> entries = Account.Entries;

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

    private void UpdateDateStartFromLoadedEntries(DateTime requestedDateStart)
    {
        var oldestLoadedEntryDate = Account?.Entries.LastOrDefault()?.PostingDate;
        _dateStart = oldestLoadedEntryDate is not null && oldestLoadedEntryDate.Value < requestedDateStart
            ? oldestLoadedEntryDate.Value
            : requestedDateStart;
    }

    private RecentEntriesLoaderOptions<BondAccount> CreateLoaderOptions() =>
        new()
        {
            LoadByDateRangeAsync = (dateStart, dateEnd) => _user is null
                ? Task.FromResult<BondAccount?>(null)
                : FinancialAccountService.GetAccount<BondAccount>(_user.UserId, AccountId, dateStart, dateEnd),
            GetEntryCount = account => account.Entries.Count,
            GetNextOlderReferenceDate = account => account.NextOlderEntries.Values.Select(x => (DateTime?)x.PostingDate).Max(),
            HasOlderEntries = account => account.NextOlderEntries.Any(),
        };
}
