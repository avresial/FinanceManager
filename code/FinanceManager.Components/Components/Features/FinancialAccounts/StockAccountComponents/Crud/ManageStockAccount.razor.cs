using FinanceManager.Components.Components.Shared;
using FinanceManager.Components.Components.Shared.Dialogs;
using FinanceManager.Components.HttpClients;
using FinanceManager.Components.Services;
using FinanceManager.Domain.Entities.Stocks;
using FinanceManager.Domain.Identity.Services;
using FinanceManager.Domain.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using MudBlazor;

namespace FinanceManager.Components.Components.Features.FinancialAccounts.StockAccountComponents.Crud;

public partial class ManageStockAccount : ComponentBase
{
    private MudForm? _form;
    private bool _success;
    private string[] _errors = [];

    private StockAccount? _stockAccount;
    private bool _isRecalculating;

    public string AccountName { get; set; } = string.Empty;

    [Parameter] public required int AccountId { get; set; }

    [Inject] public required IFinancialAccountService FinancialAccountService { get; set; }
    [Inject] public required NavigationManager Navigation { get; set; }
    [Inject] public required IDialogService DialogService { get; set; }
    [Inject] public required ILoginService LoginService { get; set; }
    [Inject] public required AccountDataSynchronizationService AccountDataSynchronizationService { get; set; }
    [Inject] public required ILogger<ManageStockAccount> Logger { get; set; }
    [Inject] public required StockEntryHttpClient StockEntryHttpClient { get; set; }
    [Inject] public required ISnackbar Snackbar { get; set; }

    protected override async Task OnParametersSetAsync()
    {
        try
        {
            var user = await LoginService.GetLoggedUser();
            if (user is null) return;

            _stockAccount = await FinancialAccountService.GetAccount<StockAccount>(user.UserId, AccountId, DateTime.UtcNow, DateTime.UtcNow);

            if (_stockAccount is null) return;

            AccountName = _stockAccount.Name;
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
            if (_stockAccount is null) return;
            if (string.IsNullOrEmpty(AccountName))
            {
                _errors = [$"AccountName can not be empty"];
                return;
            }

            if (_stockAccount is null) return;

            StockAccount updatedAccount = new(_stockAccount.UserId, _stockAccount.AccountId, AccountName);
            await FinancialAccountService.UpdateAccount(updatedAccount);
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
                await FinancialAccountService.RemoveAccount(AccountId);
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

    public async Task Recalculate()
    {
        if (_isRecalculating || _stockAccount is null) return;

        try
        {
            _isRecalculating = true;

            if (await StockEntryHttpClient.RecalculateBalanceAsync(AccountId))
            {
                Snackbar.Add("Account balance recalculated.", Severity.Success);
                await AccountDataSynchronizationService.AccountChanged();
            }
            else
            {
                Snackbar.Add("Failed to recalculate account balance.", Severity.Error);
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error recalculating balance for Stock Account with AccountId {AccountId}", AccountId);
            Snackbar.Add("Failed to recalculate account balance.", Severity.Error);
        }
        finally
        {
            _isRecalculating = false;
        }
    }
}