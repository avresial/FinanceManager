using FinanceManager.Components.Features.Dashboard.Models;
using FinanceManager.Components.Features.Dashboard.Services;
using FinanceManager.Components.Features.FinancialAccounts.HttpClients;
using FinanceManager.Components.Features.Identity.Services;
using FinanceManager.Components.Features.Labels.HttpClients;
using FinanceManager.Components.Shared.Services;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.FinancialAccounts.Shared.Services;
using FinanceManager.Domain.Identity.Services;
using FinanceManager.Domain.Labels.Entities;
using FinanceManager.Domain.MoneyFlow.Entities;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using MudBlazor;

namespace FinanceManager.Components.Features.Dashboard.Components.Cards;

public partial class RecurringTransactionDetectorCard
{
    private bool _isLoading;
    private bool _isLoadingDetail;
    private List<RecurringTransactionResult> _data = [];
    private decimal _totalMonthlySpend;
    private RecurringTransactionResult? _selectedItem;
    private List<CurrencyAccountEntry> _detailEntries = [];
    private int _detailRequestVersion;
    private readonly RefreshVersionGate _refreshGate = new();

    [Parameter] public string Height { get; set; } = "300px";

    [Inject] public required ILogger<RecurringTransactionDetectorCard> Logger { get; set; }
    [Inject] public required ISnackbar Snackbar { get; set; }
    [Inject] public required RecurringTransactionDetectorHttpClient RecurringTransactionDetectorHttpClient { get; set; }
    [Inject] public required ILoginService LoginService { get; set; }
    [Inject] public required CurrencyEntryHttpClient CurrencyEntryHttpClient { get; set; }
    [Inject] public required DashboardCardsSnapshotStore SnapshotStore { get; set; }

    protected override async Task OnInitializedAsync()
    {
        await LoadData();
    }

    private async Task LoadData()
    {
        var version = _refreshGate.Claim();
        var user = await LoginService.GetLoggedUser();
        if (!_refreshGate.IsCurrent(version)) return;
        if (user is null)
        {
            _data = [];
            _totalMonthlySpend = 0;
            _isLoading = false;
            return;
        }

        var result = await SnapshotStore.RefreshRecurringTransactionsAsync(
            user.UserId,
            _refreshGate,
            version,
            async () =>
            {
                var data = await RecurringTransactionDetectorHttpClient.GetRecurringTransactions(user.UserId);
                return new(data, data.Count == 0 ? 0 : Math.Round(data.Sum(x => x.Value), 2));
            },
            onSnapshotPainted: ShowData,
            onSnapshotMissing: ShowLoading,
            onRefreshed: ShowData);
        if (!_refreshGate.IsCurrent(version)) return;

        _isLoading = false;
        if (result.IsBlockingFailure)
        {
            Logger.LogError(result.Error, "Unable to load recurring transactions.");
            Snackbar.Add("Unable to load recurring transactions.", Severity.Error);
        }
        StateHasChanged();
    }

    private Task ShowData(RecurringTransactionsCardModel model)
    {
        _data = model.Data;
        _totalMonthlySpend = model.TotalMonthlySpend;
        _isLoading = false;
        return InvokeAsync(StateHasChanged);
    }

    private Task ShowLoading()
    {
        _isLoading = true;
        return InvokeAsync(StateHasChanged);
    }

    private async Task SelectItem(RecurringTransactionResult item)
    {
        var requestVersion = Interlocked.Increment(ref _detailRequestVersion);
        _selectedItem = item;
        _detailEntries = [];
        _isLoadingDetail = true;
        StateHasChanged();

        try
        {
            var tasks = item.Entries.Select(r => CurrencyEntryHttpClient.GetEntry(r.AccountId, r.EntryId));
            var results = await Task.WhenAll(tasks);

            if (requestVersion != _detailRequestVersion) return;

            _detailEntries = [.. results.OfType<CurrencyAccountEntry>().OrderByDescending(e => e.PostingDate)];
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to load entries for {Name}", item.Name);
            if (requestVersion == _detailRequestVersion)
                Snackbar.Add("Unable to load transaction details.", Severity.Error);
        }
        finally
        {
            if (requestVersion == _detailRequestVersion)
            {
                _isLoadingDetail = false;
                StateHasChanged();
            }
        }
    }

    private void Back()
    {
        Interlocked.Increment(ref _detailRequestVersion);
        _selectedItem = null;
        _detailEntries = [];
        _isLoadingDetail = false;
    }
}