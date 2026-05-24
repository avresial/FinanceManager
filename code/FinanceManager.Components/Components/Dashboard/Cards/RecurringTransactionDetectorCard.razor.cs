using FinanceManager.Components.HttpClients;
using FinanceManager.Domain.Entities.FinancialAccounts.Currencies;
using FinanceManager.Domain.Entities.MoneyFlowModels;
using FinanceManager.Domain.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;

namespace FinanceManager.Components.Components.Dashboard.Cards;

public partial class RecurringTransactionDetectorCard
{
    private bool _isLoading;
    private bool _isLoadingDetail;
    private List<RecurringTransactionResult> _data = [];
    private decimal _totalMonthlySpend;
    private RecurringTransactionResult? _selectedItem;
    private List<CurrencyAccountEntry> _detailEntries = [];

    [Parameter] public string Height { get; set; } = "300px";

    [Inject] public required ILogger<RecurringTransactionDetectorCard> Logger { get; set; }
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
        }
        finally
        {
            _isLoading = false;
            StateHasChanged();
        }
    }

    private async Task SelectItem(RecurringTransactionResult item)
    {
        _selectedItem = item;
        _detailEntries = [];
        _isLoadingDetail = true;
        StateHasChanged();

        try
        {
            var tasks = item.Entries.Select(r => CurrencyEntryHttpClient.GetEntry(r.AccountId, r.EntryId));
            var results = await Task.WhenAll(tasks);
            _detailEntries = [.. results.OfType<CurrencyAccountEntry>().OrderByDescending(e => e.PostingDate)];
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to load entries for {Name}", item.Name);
        }
        finally
        {
            _isLoadingDetail = false;
            StateHasChanged();
        }
    }

    private void Back()
    {
        _selectedItem = null;
        _detailEntries = [];
    }

    private double GetPercentage(decimal value)
    {
        if (_data.Count == 0) return 0;
        var max = _data.Max(x => x.Value);
        return max == 0 ? 0 : (double)(value / max * 100);
    }
}
