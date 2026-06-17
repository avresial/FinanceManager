using FinanceManager.Components.Components.Shared;
using FinanceManager.Components.Components.Shared.Dialogs;
using FinanceManager.Components.HttpClients;
using FinanceManager.Components.Services;
using FinanceManager.Domain.Entities.Bonds;
using FinanceManager.Domain.Identity.Services;
using FinanceManager.Domain.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using MudBlazor;

namespace FinanceManager.Components.Components.Features.FinancialAccounts.BondAccountComponents.Crud;

public partial class ManageBondAccount
{
    private MudForm? _form;
    private bool _success;
    private string[] _errors = [];
    private BondAccount? _bondAccount = null;
    private bool _isRecalculating;

    public string AccountName { get; set; } = string.Empty;

    [Parameter] public required int AccountId { get; set; }

    [Inject] public required IFinancialAccountService FinancialAccountService { get; set; }
    [Inject] public required NavigationManager Navigation { get; set; }
    [Inject] public required IDialogService DialogService { get; set; }
    [Inject] public required ILoginService LoginService { get; set; }
    [Inject] public required ILogger<ManageBondAccount> Logger { get; set; }
    [Inject] public required AccountDataSynchronizationService AccountDataSynchronizationService { get; set; }
    [Inject] public required BondEntryHttpClient BondEntryHttpClient { get; set; }
    [Inject] public required ISnackbar Snackbar { get; set; }

    protected override async Task OnParametersSetAsync()
    {
        try
        {
            var user = await LoginService.GetLoggedUser();
            if (user is null) return;

            _bondAccount = await FinancialAccountService.GetAccount<BondAccount>(user.UserId, AccountId, DateTime.UtcNow, DateTime.UtcNow);

            if (_bondAccount is null) return;

            AccountName = _bondAccount.Name;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error loading bond account with ID {AccountId}", AccountId);
        }
    }

    public async Task Update()
    {
        try
        {

            if (_form is null) return;
            await _form.Validate();

            if (!_form.IsValid) return;
            if (_bondAccount is null) return;
            if (string.IsNullOrEmpty(AccountName))
            {
                _errors = [$"AccountName can not be empty"];
                return;
            }

            if (_bondAccount is null) return;

            BondAccount updatedAccount = new(_bondAccount.UserId, _bondAccount.AccountId, AccountName);
            await FinancialAccountService.UpdateAccount(updatedAccount);
            await AccountDataSynchronizationService.AccountChanged();
            Navigation.NavigateTo($"AccountDetails/{AccountId}");
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error updating bond account with ID {AccountId}", AccountId);
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
            Logger.LogError(ex, "Error removing bond account with ID {AccountId}", AccountId);
        }
    }

    public async Task Recalculate()
    {
        if (_isRecalculating || _bondAccount is null) return;

        try
        {
            _isRecalculating = true;

            if (await BondEntryHttpClient.RecalculateBalanceAsync(AccountId))
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
            Logger.LogError(ex, "Error recalculating balance for bond account with ID {AccountId}", AccountId);
            Snackbar.Add("Failed to recalculate account balance.", Severity.Error);
        }
        finally
        {
            _isRecalculating = false;
        }
    }
}