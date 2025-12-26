
using FinanceManager.Components.Components.SharedComponents;
using FinanceManager.Components.Services;
using FinanceManager.Domain.Entities.Cash;
using FinanceManager.Domain.Enums;
using FinanceManager.Domain.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using MudBlazor;

namespace FinanceManager.Components.Components.FinancialAccounts.BankAccountComponents;

public partial class ManageBankAccount
{
    private MudForm? _form;
    private bool _success;
    private string[] _errors = [];
    private BankAccount? _bankAccount = null;

    public string AccountName { get; set; } = string.Empty;
    public AccountLabel AccountType { get; set; }

    [Parameter] public required int AccountId { get; set; }

    [Inject] public required IFinancialAccountService FinancalAccountService { get; set; }
    [Inject] public required NavigationManager Navigation { get; set; }
    [Inject] public required IDialogService DialogService { get; set; }
    [Inject] public required ILoginService LoginService { get; set; }
    [Inject] public required ILogger<ManageBankAccount> Logger { get; set; }
    [Inject] public required AccountDataSynchronizationService AccountDataSynchronizationService { get; set; }

    protected override async Task OnParametersSetAsync()
    {
        try
        {
            var user = await LoginService.GetLoggedUser();
            if (user is null) return;

            _bankAccount = await FinancalAccountService.GetAccount<BankAccount>(user.UserId, AccountId, DateTime.UtcNow, DateTime.UtcNow);

            if (_bankAccount is null) return;

            AccountName = _bankAccount.Name;
            AccountType = _bankAccount.AccountType;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error loading bank account with ID {AccountId}", AccountId);
        }
    }

    public async Task Update()
    {
        try
        {

            if (_form is null) return;
            await _form.Validate();

            if (!_form.IsValid) return;
            if (_bankAccount is null) return;
            if (string.IsNullOrEmpty(AccountName))
            {
                _errors = [$"AccountName can not be empty"];
                return;
            }

            if (_bankAccount is null) return;

            BankAccount updatedAccount = new BankAccount(_bankAccount.UserId, _bankAccount.AccountId, AccountName, AccountType);
            await FinancalAccountService.UpdateAccount(updatedAccount);
            await AccountDataSynchronizationService.AccountChanged();
            Navigation.NavigateTo($"AccountDetails/{AccountId}");
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error updating bank account with ID {AccountId}", AccountId);
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
            Logger.LogError(ex, "Error removing bank account with ID {AccountId}", AccountId);
        }
    }
}