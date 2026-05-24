using FinanceManager.Components.HttpClients;
using FinanceManager.Domain.Entities.MoneyFlowModels;
using FinanceManager.Domain.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using MudBlazor;

namespace FinanceManager.Components.Components.Dashboard.Cards;

public partial class RecurringTransactionDetectorCard
{
    private bool _isLoading;
    private List<RecurringTransactionResult> _data = [];
    private decimal _totalMonthlySpend;

    [Parameter] public string Height { get; set; } = "300px";

    [Inject] public required ILogger<RecurringTransactionDetectorCard> Logger { get; set; }
    [Inject] public required RecurringTransactionDetectorHttpClient RecurringTransactionDetectorHttpClient { get; set; }
    [Inject] public required ILoginService LoginService { get; set; }
    [Inject] public required IDialogService DialogService { get; set; }

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

    private async Task ShowEntries(RecurringTransactionResult item)
    {
        var parameters = new DialogParameters
        {
            { nameof(RecurringTransactionEntriesDialog.Name), item.Name },
            { nameof(RecurringTransactionEntriesDialog.EntryReferences), item.Entries }
        };

        var options = new DialogOptions { MaxWidth = MaxWidth.Medium, FullWidth = true, CloseOnEscapeKey = true };
        await DialogService.ShowAsync<RecurringTransactionEntriesDialog>(item.Name, parameters, options);
    }

    private double GetPercentage(decimal value)
    {
        if (_data.Count == 0) return 0;
        var max = _data.Max(x => x.Value);
        return max == 0 ? 0 : (double)(value / max * 100);
    }
}
