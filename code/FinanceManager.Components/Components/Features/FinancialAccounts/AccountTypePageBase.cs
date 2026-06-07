using FinanceManager.Components.Services;
using Microsoft.AspNetCore.Components;

namespace FinanceManager.Components.Components.Features.FinancialAccounts;

public abstract class AccountTypePageBase : ComponentBase
{
    public ElementReference MyElementReference;

    [Parameter] public required int AccountId { get; set; }

    [Inject] public required IFinancialAccountService FinancialAccountService { get; set; }

    public FinancialAccountTypeDescriptor? AccountDescriptor { get; private set; }
    public Type? AccountType => AccountDescriptor?.AccountType;
    public string ErrorMessage { get; protected set; } = string.Empty;
    public bool IsLoading { get; private set; } = true;

    protected override async Task OnParametersSetAsync()
    {
        MyElementReference = default;
        AccountDescriptor = null;
        ErrorMessage = string.Empty;
        IsLoading = true;

        await LoadAccountDescriptor();
    }

    private async Task LoadAccountDescriptor()
    {
        try
        {
            var accounts = await FinancialAccountService.GetAvailableAccounts();

            if (!accounts.TryGetValue(AccountId, out var accountType))
            {
                ErrorMessage = $"Account {AccountId} was not found.";
                return;
            }

            AccountDescriptor = FinancialAccountTypeDescriptor.FromType(accountType);
            if (AccountDescriptor is null)
                ErrorMessage = $"{accountType} type is not supported.";
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsLoading = false;
        }
    }
}
