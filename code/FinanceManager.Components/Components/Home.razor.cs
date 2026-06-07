using FinanceManager.Components.Services;
using FinanceManager.Domain.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;

namespace FinanceManager.Components.Components;

public partial class Home : ComponentBase
{
    private bool _isLoading;
    private bool _showWelcome;

    [Inject] public required ILogger<Home> Logger { get; set; }
    [Inject] public required ILoginService LoginService { get; set; }
    [Inject] public required NavigationManager Navigation { get; set; }
    [Inject] public required IFinancialAccountService FinancialAccountService { get; set; }
    [Inject] public required AccountDataSynchronizationService AccountDataSynchronizationService { get; set; }


    protected override async Task OnInitializedAsync()
    {
        _isLoading = true;
        var loggedUser = await LoginService.GetLoggedUser();

        if (loggedUser is null)
        {
            Navigation.NavigateTo("login");
            return;
        }

        Dictionary<int, Type>? availableAccounts = null;
        try
        {
            availableAccounts = await FinancialAccountService.GetAvailableAccounts();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex.ToString());
        }

        if ((availableAccounts is null || availableAccounts.Count == 0) && loggedUser.UserName.ToLower() == "guest")
        {
            try
            {
                FinancialAccountService.InitializeMock();
                availableAccounts = await FinancialAccountService.GetAvailableAccounts();
            }
            catch (Exception ex)
            {
                Logger.LogError(ex.ToString());
            }

            await AccountDataSynchronizationService.AccountChanged();
        }

        if (availableAccounts is null || availableAccounts.Count == 0)
            _showWelcome = true;

        _isLoading = false;
    }
}