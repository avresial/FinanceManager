using FinanceManager.Components.HttpClients;
using FinanceManager.Domain.Entities.FinancialAccounts.Currencies;
using FinanceManager.Domain.Entities.MoneyFlowModels;
using FinanceManager.Domain.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using MudBlazor;

namespace FinanceManager.Components.Components.Features.Dashboard.Cards;

public partial class RecurringTransactionDetectorCard
{
    private bool _isLoading;
    private bool _isLoadingDetail;
    private List<RecurringTransactionResult> _data = [];
    private decimal _totalMonthlySpend;
    private RecurringTransactionResult? _selectedItem;
    private List<CurrencyAccountEntry> _detailEntries = [];
    private int _detailRequestVersion;

    [Parameter] public string Height { get; set; } = "300px";

    [Inject] public required ILogger<RecurringTransactionDetectorCard> Logger { get; set; }
    [Inject] public required ISnackbar Snackbar { get; set; }
    [Inject] public required RecurringTransactionDetectorHttpClient RecurringTransactionDetectorHttpClient { get; set; }
    [Inject] public required ILoginService LoginService { get; set; }
    [Inject] public required CurrencyEntryHttpClient CurrencyEntryHttpClient { get; set; }

    protected override async Task OnInitializedAsync()
    {
        await LoadData();
    }

    private async Task LoadData()
    {
        _isLoading = true;
        StateHasChanged();

        try
        {
            var user = await LoginService.GetLoggedUser();
            if (user is null)
            {
                _data = [];
                _totalMonthlySpend = 0;
                return;
            }

            _data = await RecurringTransactionDetectorHttpClient.GetRecurringTransactions(user.UserId);
            _totalMonthlySpend = _data.Count == 0 ? 0 : Math.Round(_data.Sum(x => x.Value), 2);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, ex.Message);
            Snackbar.Add("Unable to load recurring transactions.", Severity.Error);
        }
        finally
        {
            _isLoading = false;
            StateHasChanged();
        }
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

    private double GetPercentage(decimal value)
    {
        if (_data.Count == 0) return 0;
        var max = _data.Max(x => x.Value);
        return max == 0 ? 0 : (double)(value / max * 100);
    }
}