using FinanceManager.Components.Services;
using Microsoft.AspNetCore.Components;

namespace FinanceManager.WebUi.Pages.Account;

public partial class Import : ComponentBase
{
    public ElementReference MyElementReference;

    public Type? accountType = null;

    [Parameter] public required int AccountId { get; set; }

    [Inject] public required IFinancialAccountService FinancalAccountService { get; set; }

    public string ErrorMessage { get; set; } = string.Empty;

    protected override async Task OnInitializedAsync()
    {
        await UpdateEntries();
    }
    protected override async Task OnParametersSetAsync()
    {
        MyElementReference = default;
        accountType = null;
        await UpdateEntries();
    }

    private async Task UpdateEntries()
    {
        try
        {
            var accounts = await FinancalAccountService.GetAvailableAccounts();
            if (accounts.ContainsKey(AccountId))
                accountType = accounts[AccountId];

        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
    }
}
