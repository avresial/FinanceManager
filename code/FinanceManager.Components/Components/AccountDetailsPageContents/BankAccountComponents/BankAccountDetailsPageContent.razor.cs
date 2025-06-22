using FinanceManager.Components.Services;
using FinanceManager.Domain.Entities.Accounts;
using FinanceManager.Domain.Entities.Accounts.Entries;
using FinanceManager.Domain.Entities.Login;
using FinanceManager.Domain.Entities.MoneyFlowModels;
using FinanceManager.Domain.Services;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace FinanceManager.Components.Components.AccountDetailsPageContents.BankAccountComponents;

public partial class BankAccountDetailsPageContent : ComponentBase
{
    private bool _isLoadingMore = false;
    private decimal _balanceChange = 100;
    private bool _loadedAllData = false;
    private DateTime _dateStart;
    private DateTime _dateEnd = DateTime.UtcNow;
    private DateTime? _oldestEntryDate;
    private DateTime? _youngestEntryDate;

    private bool _addEntryVisibility;

    private List<BankAccountEntry>? _top5;
    private List<BankAccountEntry>? _bottom5;
    private string _currency = "PLN";
    private UserSession? _user;


    public bool IsLoading = false;
    public BankAccount? Account { get; set; }
    public string ErrorMessage { get; set; } = string.Empty;
    public List<TimeSeriesModel> ChartData { get; set; } = [];

    [Parameter] public required int AccountId { get; set; }

    [Inject] public required IFinancialAccountService FinancialAccountService { get; set; }
    [Inject] public required AccountDataSynchronizationService AccountDataSynchronizationService { get; set; }
    [Inject] public required ISettingsService settingsService { get; set; }
    [Inject] public required ILoginService loginService { get; set; }

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

        await UpdateDates();

        if (Account.Entries is not null && Account.Entries.Any() && _oldestEntryDate is not null)
            _loadedAllData = (_oldestEntryDate >= Account.Entries.Last().PostingDate);

        if (Account.Entries is null || Account.Entries.Count == 0) return;

        var EntriesOrdered = Account.Entries.OrderByDescending(x => x.ValueChange);
        _top5 = EntriesOrdered.Where(x => x.ValueChange > 0).Take(5).ToList();
        _bottom5 = EntriesOrdered.Skip(Account.Entries.Count - 5)
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
        _dateStart = _dateStart.AddMonths(-1);

        int entriesCountBeforeUpdate = 0;
        if (Account.Entries is not null) entriesCountBeforeUpdate = Account.Entries.Count();

        Account = await FinancialAccountService.GetAccount<BankAccount>(_user.UserId, AccountId, _dateStart, _dateEnd);

        if (Account is not null && Account.Entries is not null && Account.Entries.Count == entriesCountBeforeUpdate)
        {
            if (Account.NextOlderEntry is not null)
            {
                _dateStart = Account.NextOlderEntry.PostingDate;
                Account = await FinancialAccountService.GetAccount<BankAccount>(_user.UserId, AccountId, _dateStart, _dateEnd);
            }
        }
        await UpdateChartData();

        await UpdateInfo();
        _isLoadingMore = false;
    }

    protected override async Task OnInitializedAsync()
    {
        _dateStart = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);
        await UpdateEntries();

        AccountDataSynchronizationService.AccountsChanged += AccountDataSynchronizationService_AccountsChanged;
    }
    protected override async Task OnParametersSetAsync()
    {
        IsLoading = true;
        _user = await loginService.GetLoggedUser();
        if (_user is null) return;

        _loadedAllData = false;
        await UpdateEntries();
        IsLoading = false;
    }

    private async Task UpdateEntries()
    {
        try
        {
            var accounts = await FinancialAccountService.GetAvailableAccounts();
            if (accounts.ContainsKey(AccountId))
            {
                var accountType = accounts[AccountId];
                if (accountType == typeof(BankAccount))
                {
                    await UpdateDates();
                    if (_user is not null)
                    {
                        Account = await FinancialAccountService.GetAccount<BankAccount>(_user.UserId, AccountId, _dateStart, DateTime.UtcNow);
                        if (Account is not null && Account.Entries is not null)
                            await UpdateInfo();
                    }
                }
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }

        await Task.CompletedTask;
    }
    private async Task UpdateChartData()
    {
        ChartData.Clear();

        if (Account is null || Account.Entries is null) return;

        decimal previousValue = 0;

        bool initialZero = true;
        for (DateTime date = _dateStart; date <= _dateEnd; date = date.AddDays(1))
        {
            var entries = Account.Entries.Where(x => x.PostingDate.Date == date.Date).ToList();
            if (date == _dateStart && entries.Count == 0 && Account.NextOlderEntry is not null)
            {
                var olderAccount = (await FinancialAccountService.GetAccount<BankAccount>(_user.UserId, AccountId, Account.NextOlderEntry.PostingDate,
                    Account.NextOlderEntry.PostingDate.Date.AddDays(1).AddTicks(-1)));

                if (olderAccount is not null)
                {
                    var olderEntries = olderAccount.Entries;
                    if (olderEntries is not null)
                        entries = olderEntries;
                }
            }

            decimal value = 0;
            if (entries.Count != 0) value = entries.Max(x => x.Value);

            if (value != 0 && initialZero) initialZero = false;
            if (initialZero) continue;

            ChartData.Add(new()
            {
                DateTime = date,
                Value = entries.Count != 0 ? value : previousValue,
            });

            if (entries.Count != 0) previousValue = value;
        }
    }
    private async Task UpdateDates()
    {
        _oldestEntryDate = await FinancialAccountService.GetStartDate(AccountId);
        _youngestEntryDate = await FinancialAccountService.GetEndDate(AccountId);

        if (_youngestEntryDate is not null && _dateStart > _youngestEntryDate)
            _dateStart = new DateTime(_youngestEntryDate.Value.Date.Year, _youngestEntryDate.Value.Date.Month, 1);
    }
    private void AccountDataSynchronizationService_AccountsChanged()
    {
        Task.Run(async () =>
        {
            await UpdateEntries();
            await InvokeAsync(StateHasChanged);
        });
    }
}