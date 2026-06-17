using FinanceManager.Components.Components.Shared;
using FinanceManager.Components.Components.Shared.Dialogs;
using FinanceManager.Components.HttpClients;
using FinanceManager.Components.Services;
using FinanceManager.Domain.Enums;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.Identity.Services;
using FinanceManager.Domain.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using MudBlazor;

namespace FinanceManager.Components.Components.Features.FinancialAccounts.CurrencyAccountComponents.Crud;

public partial class ManageCurrencyAccount
{
    private MudForm? _form;
    private bool _success;
    private string[] _errors = [];
    private CurrencyAccount? _currencyAccount = null;
    private bool _isRecalculating;

    public string AccountName { get; set; } = string.Empty;
    public AccountLabel AccountType { get; set; }

    [Parameter] public required int AccountId { get; set; }

    [Inject] public required IFinancialAccountService FinancialAccountService { get; set; }
    [Inject] public required NavigationManager Navigation { get; set; }
    [Inject] public required IDialogService DialogService { get; set; }
    [Inject] public required ILoginService LoginService { get; set; }
    [Inject] public required ILogger<ManageCurrencyAccount> Logger { get; set; }
    [Inject] public required AccountDataSynchronizationService AccountDataSynchronizationService { get; set; }
    [Inject] public required CurrencyEntryHttpClient CurrencyEntryHttpClient { get; set; }
    [Inject] public required ISnackbar Snackbar { get; set; }

    protected override async Task OnParametersSetAsync()
    {
        try
        {
            var user = await LoginService.GetLoggedUser();
            if (user is null) return;

            _currencyAccount = await FinancialAccountService.GetAccount<CurrencyAccount>(user.UserId, AccountId, DateTime.UtcNow, DateTime.UtcNow);

            if (_currencyAccount is null) return;

            AccountName = _currencyAccount.Name;
            AccountType = _currencyAccount.AccountType;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error loading currency account with ID {AccountId}", AccountId);
        }
    }

    public async Task Update()
    {
        try
        {

            if (_form is null) return;
            await _form.Validate();

            if (!_form.IsValid) return;
            if (_currencyAccount is null) return;
            if (string.IsNullOrEmpty(AccountName))
            {
                _errors = [$"AccountName can not be empty"];
                return;
            }

            if (_currencyAccount is null) return;

            CurrencyAccount updatedAccount = new CurrencyAccount(_currencyAccount.UserId, _currencyAccount.AccountId, AccountName, AccountType);
            await FinancialAccountService.UpdateAccount(updatedAccount);
            await AccountDataSynchronizationService.AccountChanged();
            Navigation.NavigateTo($"AccountDetails/{AccountId}");
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error updating currency account with ID {AccountId}", AccountId);
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
            Logger.LogError(ex, "Error removing currency account with ID {AccountId}", AccountId);
        }
    }

    public async Task Recalculate()
    {
        if (_isRecalculating || _currencyAccount is null) return;

        try
        {
            _isRecalculating = true;

            if (await CurrencyEntryHttpClient.RecalculateBalanceAsync(AccountId))
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
            Logger.LogError(ex, "Error recalculating balance for currency account with ID {AccountId}", AccountId);
            Snackbar.Add("Failed to recalculate account balance.", Severity.Error);
        }
        finally
        {
            _isRecalculating = false;
        }
    }
}