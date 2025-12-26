using FinanceManager.Components.Components.SharedComponents;
using FinanceManager.Components.Services;
using FinanceManager.Domain.Entities.Stocks;
using FinanceManager.Domain.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using MudBlazor;

namespace FinanceManager.Components.Components.FinancialAccounts.StockAccountComponents;

public partial class ManageStockAccount : ComponentBase
{
    private MudForm? _form;
    private bool _success;
    private string[] _errors = [];

    private StockAccount? StocktAccount { get; set; } = null;

    public string AccountName { get; set; } = string.Empty;

    [Parameter] public required int AccountId { get; set; }

    [Inject] public required IFinancialAccountService FinancalAccountService { get; set; }
    [Inject] public required NavigationManager Navigation { get; set; }
    [Inject] public required IDialogService DialogService { get; set; }
    [Inject] public required ILoginService LoginService { get; set; }
    [Inject] public required AccountDataSynchronizationService AccountDataSynchronizationService { get; set; }
    [Inject] public required ILogger<ManageStockAccount> Logger { get; set; }

    protected override async Task OnParametersSetAsync()
    {
        try
        {
            var user = await LoginService.GetLoggedUser();
            if (user is null) return;

            StocktAccount = await FinancalAccountService.GetAccount<StockAccount>(user.UserId, AccountId, DateTime.UtcNow, DateTime.UtcNow);

            if (StocktAccount is null) return;

            AccountName = StocktAccount.Name;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error loading Stock Account with AccountId {AccountId}", AccountId);
            _errors = [$"An error occurred while loading the account: {ex.Message}"];
        }
    }

    public async Task Update()
    {
        try
        {
            if (_form is null) return;
            await _form.Validate();

            if (!_form.IsValid) return;
            if (StocktAccount is null) return;
            if (string.IsNullOrEmpty(AccountName))
            {
                _errors = [$"AccountName can not be empty"];
                return;
            }

            if (StocktAccount is null) return;

            StockAccount updatedAccount = new StockAccount(StocktAccount.UserId, StocktAccount.AccountId, AccountName);
            await FinancalAccountService.UpdateAccount(updatedAccount);
            await AccountDataSynchronizationService.AccountChanged();
            Navigation.NavigateTo($"AccountDetails/{AccountId}");
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error updating Stock Account with AccountId {AccountId}", AccountId);
            _errors = [ex.Message];
        }
    }

    public async Task Remove()
    {
        try
        {

            var options = new DialogOptions { CloseOnEscapeKey = true };
            var dialog = await DialogService.ShowAsync<ConfirmRemoveDialog>("Simple Dialog", options);
            var result = await dialog.Result;

            if (result is not null && !result.Canceled)
            {
                await FinancalAccountService.RemoveAccount(AccountId);
                Navigation.NavigateTo($"");
                await AccountDataSynchronizationService.AccountChanged();
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error removing Stock Account with AccountId {AccountId}", AccountId);
            _errors = [ex.Message];
        }
    }
}