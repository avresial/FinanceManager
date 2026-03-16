using FinanceManager.Components.Models;
using FinanceManager.Components.Services;
using FinanceManager.Domain.Entities.Users;
using FinanceManager.Domain.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;

namespace FinanceManager.Components.Components.Layout;

public partial class NavMenu : ComponentBase, IDisposable
{
    private bool _displayAssetsLink = false;
    private bool _displayLiabilitiesLink = false;
    private int? _currentUserId;
    private bool _isDisposed;

    [Parameter] public bool DrawerIsOpen { get; set; }
    [Inject] public required NavMenuStateCacheService NavMenuStateCacheService { get; set; }
    [Inject] public required AccountDataSynchronizationService AccountDataSynchronizationService { get; set; }
    [Inject] public required ILoginService LoginService { get; set; }
    [Inject] public required ILogger<NavMenu> Logger { get; set; }


    public Dictionary<int, string> Accounts = [];
    public string ErrorMessage { get; set; } = string.Empty;

    protected override async Task OnInitializedAsync()
    {
        AccountDataSynchronizationService.AccountsChanged += AccountDataSynchronizationService_AccountsChanged;
        LoginService.LogginStateChanged += LoginService_LogginStateChanged;

        try
        {
            await LoadCachedStateAndRefreshAsync(forceRefresh: false);
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
    }

    private void AccountDataSynchronizationService_AccountsChanged() => _ = InvokeAsync(RefreshAfterAccountChangeAsync);

    private void LoginService_LogginStateChanged(bool isLoggedIn) => _ = InvokeAsync(() => HandleLoginStateChangedAsync(isLoggedIn));

    private async Task HandleLoginStateChangedAsync(bool isLoggedIn)
    {
        if (!isLoggedIn)
        {
            _currentUserId = null;
            ClearState();
            await InvokeAsync(StateHasChanged);
            return;
        }

        await LoadCachedStateAndRefreshAsync(forceRefresh: false);
    }

    private async Task RefreshAfterAccountChangeAsync()
    {
        if (_currentUserId.HasValue)
            await NavMenuStateCacheService.InvalidateAsync(_currentUserId.Value);

        await LoadCachedStateAndRefreshAsync(forceRefresh: true);
    }

    private async Task LoadCachedStateAndRefreshAsync(bool forceRefresh)
    {
        var user = await TryGetLoggedUserAsync();
        if (user is null)
        {
            _currentUserId = null;
            ClearState();
            return;
        }

        _currentUserId = user.UserId;

        if (!forceRefresh)
        {
            var cachedSnapshot = await NavMenuStateCacheService.GetCachedSnapshotAsync(user.UserId);
            if (cachedSnapshot is not null)
            {
                ApplySnapshot(cachedSnapshot);
                await InvokeAsync(StateHasChanged);
                _ = RefreshSnapshotAsync(user);
                return;
            }
        }

        await RefreshSnapshotAsync(user);
    }

    private async Task<UserSession?> TryGetLoggedUserAsync()
    {
        ErrorMessage = string.Empty;

        try
        {
            return await LoginService.GetLoggedUser();
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            Logger.LogError(ex, "Error while getting logged user for nav menu");
            return null;
        }
    }

    private async Task RefreshSnapshotAsync(UserSession user)
    {
        try
        {
            var snapshot = await NavMenuStateCacheService.RefreshAsync(user);
            if (_currentUserId != user.UserId || _isDisposed)
                return;

            ApplySnapshot(snapshot);
            ErrorMessage = string.Empty;
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            Logger.LogError(ex, "Error while refreshing nav menu snapshot");
        }

        if (!_isDisposed)
            await InvokeAsync(StateHasChanged);
    }

    private void ApplySnapshot(NavMenuCacheSnapshot snapshot)
    {
        Accounts = snapshot.Accounts.ToDictionary(account => account.AccountId, account => account.Name);
        _displayAssetsLink = snapshot.DisplayAssetsLink;
        _displayLiabilitiesLink = snapshot.DisplayLiabilitiesLink;
    }

    private void ClearState()
    {
        ErrorMessage = string.Empty;
        Accounts.Clear();
        _displayAssetsLink = false;
        _displayLiabilitiesLink = false;
    }

    public void Dispose()
    {
        _isDisposed = true;
        AccountDataSynchronizationService.AccountsChanged -= AccountDataSynchronizationService_AccountsChanged;
        LoginService.LogginStateChanged -= LoginService_LogginStateChanged;
    }
}