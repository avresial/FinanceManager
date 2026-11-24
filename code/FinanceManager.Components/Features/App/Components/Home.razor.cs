using FinanceManager.Components.Shared.Services;
using FinanceManager.Domain.Identity.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;

namespace FinanceManager.Components.Features.App.Components;

public partial class Home : ComponentBase
{
    private bool _isLoading;
    private bool _showWelcome;
    private bool _hasLoadingError;

    [Inject] public required ILogger<Home> Logger { get; set; }
    [Inject] public required ILoginService LoginService { get; set; }
    [Inject] public required NavigationManager Navigation { get; set; }
    [Inject] public required NavMenuStateCacheService NavMenuStateCache { get; set; }

    protected override Task OnInitializedAsync() => LoadAsync();

    private async Task LoadAsync()
    {
        if (_isLoading)
            return;

        _isLoading = true;
        _hasLoadingError = false;
        try
        {
            var loggedUser = await LoginService.GetLoggedUser();
            if (loggedUser is null)
            {
                Navigation.NavigateTo("login");
                return;
            }

            // A cached "has transactions" skips the request: it only goes stale once the user deletes every transaction,
            // which just leaves the dashboard on screen. Anything else is re-checked, so a first add shows the dashboard.
            var cached = await NavMenuStateCache.GetCachedSnapshotAsync(loggedUser.UserId);
            var hasTransactions = cached?.HasTransactions == true
                || (await NavMenuStateCache.RefreshAsync(loggedUser)).HasTransactions;
            _showWelcome = !hasTransactions;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to decide between the dashboard and the welcome page");
            _hasLoadingError = true;
        }
        finally
        {
            _isLoading = false;
        }
    }
}