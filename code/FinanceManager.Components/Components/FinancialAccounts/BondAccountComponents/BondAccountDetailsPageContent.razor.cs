using FinanceManager.Application.Services;
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
    private bool _isLoadingMore = false;
    private decimal? _balanceChange = null;
    private bool _loadedAllData = false;
    private DateTime _dateStart;
    private DateTime _dateEnd = DateTime.UtcNow;
    private DateTime? _oldestEntryDate;
    private DateTime? _youngestEntryDate;

    private bool _addEntryVisibility;

    private List<(BondAccountEntry, decimal)>? _top5;
    private List<(BondAccountEntry, decimal)>? _bottom5;
    private Currency _currency = DefaultCurrency.PLN;
    private UserSession? _user;

    private List<BondDetails> _bondDetails = [];

    public bool IsLoading = false;
    public BondAccount? Account { get; set; }
    public string ErrorMessage { get; set; } = string.Empty;
    public List<TimeSeriesModel> ChartData { get; set; } = [];

    [Parameter] public required int AccountId { get; set; }

    [Inject] public required AccountDataSynchronizationService AccountDataSynchronizationService { get; set; }
    [Inject] public required IFinancialAccountService FinancialAccountService { get; set; }
    [Inject] public required BondDetailsHttpClient BondDetailsHttpClient { get; set; }
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
        await UpdateDates();

        var bondIds = Account.GetStoredBondsIds();
        foreach (var id in bondIds)
        {
            if (!_bondDetails.Any(x => x.Id == id))
            {
                var bond = await BondDetailsHttpClient.GetById(id);
                if (bond is not null) _bondDetails.Add(bond);
            }
        }

        if (Account.Entries is not null && Account.Entries.Any() && _oldestEntryDate is not null)
            _loadedAllData = (_oldestEntryDate >= Account.Entries.Last().PostingDate);

        await UpdateChartData();

        if (ChartData is not null && ChartData.Count >= 2)
            _balanceChange = ChartData.Last().Value - ChartData.First().Value;


        if (Account.Entries is null) return;

        var orderedByPrice = Account.Entries
            .OrderByDescending(x => x.ValueChange)
            .Select(x => (x, x.ValueChange))
            .ToList();

        _top5 = orderedByPrice.Take(5).ToList();
        _bottom5 = orderedByPrice.Skip(Account.Entries.Count - 5).Take(5).OrderBy(x => x.Item2).ToList();
    }

    public async Task LoadMore()
    {
        try
        {
            if (Account is null || Account.Start is null) return;
            if (_user is null) return;

            _isLoadingMore = true;
            _dateStart = _dateStart.AddMonths(-1);

            int entriesCountBeforeUpdate = 0;
            if (Account.Entries is not null) entriesCountBeforeUpdate = Account.Entries.Count();

            Account = await FinancialAccountService.GetAccount<BondAccount>(_user.UserId, AccountId, _dateStart, _dateEnd);

            if (Account is not null && Account.Entries is not null && Account.Entries.Count == entriesCountBeforeUpdate)
            {
                if (Account.NextOlderEntries is not null && Account.NextOlderEntries.Any())
                {
                    _dateStart = Account.NextOlderEntries.First().Value.PostingDate;
                    Account = await FinancialAccountService.GetAccount<BondAccount>(_user.UserId, AccountId, _dateStart, _dateEnd);
                }
            }


            await UpdateChartData();
            await UpdateInfo();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error while loading more bond account details for account ID {AccountId}", AccountId);
            ErrorMessage = ex.Message;
        }
        _isLoadingMore = false;
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
            var accounts = await FinancialAccountService.GetAvailableAccounts();
            if (accounts.ContainsKey(AccountId))
            {
                var accountType = accounts[AccountId];
                if (accountType == typeof(BondAccount))
                {
                    await UpdateDates();
                    if (_user is not null)
                    {
                        Account = await FinancialAccountService.GetAccount<BondAccount>(_user.UserId, AccountId, _dateStart, DateTime.UtcNow);
                        if (Account is not null && Account.Entries is not null)
                            await UpdateInfo();
                    }
                }
            }
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

        if (Account is null || Account.Entries is null) return;
        Dictionary<DateOnly, decimal> pricesDaily = Account.GetDailyPrice(DateOnly.FromDateTime(_dateStart), DateOnly.FromDateTime(_dateEnd), _bondDetails);

        var result = pricesDaily.Select(x => new TimeSeriesModel() { DateTime = x.Key.ToDateTime(new TimeOnly()), Value = x.Value }).OrderBy(x => x.DateTime).ToList();
        if (result.Count != 0)
        {
            var timeBucket = TimeBucketService.Get(result.Select(x => (x.DateTime, x.Value)));
            ChartData.AddRange(timeBucket.Select(x => new TimeSeriesModel()
            {
                DateTime = x.Date,
                Value = x.Objects.Last(),
            }));
        }
    }

    private async Task UpdateDates()
    {
        _oldestEntryDate = await FinancialAccountService.GetStartDate(AccountId);
        _youngestEntryDate = await FinancialAccountService.GetEndDate(AccountId);

        if (_youngestEntryDate is not null && _dateStart > _youngestEntryDate)
            _dateStart = new DateTime(_youngestEntryDate.Value.Date.Year, _youngestEntryDate.Value.Date.Month, 1);
    }

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
}
