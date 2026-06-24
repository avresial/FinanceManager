using FinanceManager.Components.Components.Shared.Dialogs;
using FinanceManager.Components.Services;
using FinanceManager.Domain.FinancialAccounts.Investments.Entities;
using FinanceManager.Domain.FinancialAccounts.Shared.Services;
using FinanceManager.Domain.Identity.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using MudBlazor;

namespace FinanceManager.Components.Components.Features.FinancialAccounts.InvestmentAccountComponents;

public partial class ManageInvestmentAccount : ComponentBase
{
    private MudForm? _form;
    private bool _success;
    private string[] _errors = [];

    private InvestmentAccount? _investmentAccount;

    public string AccountName { get; set; } = string.Empty;

    [Parameter] public required int AccountId { get; set; }

    [Inject] public required IFinancialAccountService FinancialAccountService { get; set; }
    [Inject] public required NavigationManager Navigation { get; set; }
    [Inject] public required IDialogService DialogService { get; set; }
    [Inject] public required ILoginService LoginService { get; set; }
    [Inject] public required AccountDataSynchronizationService AccountDataSynchronizationService { get; set; }
    [Inject] public required ILogger<ManageInvestmentAccount> Logger { get; set; }

    protected override async Task OnParametersSetAsync()
    {
        try
        {
            var user = await LoginService.GetLoggedUser();
            if (user is null) return;

            _investmentAccount = await FinancialAccountService.GetAccount<InvestmentAccount>(user.UserId, AccountId, DateTime.UtcNow, DateTime.UtcNow);

            if (_investmentAccount is null) return;

            AccountName = _investmentAccount.Name;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error loading Investment Account with AccountId {AccountId}", AccountId);
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
            if (_investmentAccount is null) return;
            if (string.IsNullOrEmpty(AccountName))
            {
                _errors = [$"AccountName can not be empty"];
                return;
            }

            InvestmentAccount updatedAccount = new(_investmentAccount.UserId, _investmentAccount.AccountId, AccountName);
            await FinancialAccountService.UpdateAccount(updatedAccount);
            await AccountDataSynchronizationService.AccountChanged();
            Navigation.NavigateTo($"AccountDetails/{AccountId}");
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error updating Investment Account with AccountId {AccountId}", AccountId);
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
            Logger.LogError(ex, "Error removing Investment Account with AccountId {AccountId}", AccountId);
            _errors = [ex.Message];
        }
    }
}