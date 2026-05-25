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
    private readonly CancellationTokenSource _disposeTokenSource = new();

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
            await LoadCachedStateAndRefreshAsync(forceRefresh: false, _disposeTokenSource.Token);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
    }

    private void AccountDataSynchronizationService_AccountsChanged()
    {
        if (_disposeTokenSource.IsCancellationRequested)
            return;

        _ = InvokeAsync(() => RefreshAfterAccountChangeAsync(_disposeTokenSource.Token));
    }

    private void LoginService_LogginStateChanged(bool isLoggedIn)
    {
        if (_disposeTokenSource.IsCancellationRequested)
            return;

        _ = InvokeAsync(() => HandleLoginStateChangedAsync(isLoggedIn, _disposeTokenSource.Token));
    }

    private async Task HandleLoginStateChangedAsync(bool isLoggedIn, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!isLoggedIn)
        {
            _currentUserId = null;
            ClearState();
            cancellationToken.ThrowIfCancellationRequested();
            await InvokeAsync(StateHasChanged);
            return;
        }

        await LoadCachedStateAndRefreshAsync(forceRefresh: false, cancellationToken);
    }

    private async Task RefreshAfterAccountChangeAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (_currentUserId.HasValue)
            await NavMenuStateCacheService.InvalidateAsync(_currentUserId.Value);

        await LoadCachedStateAndRefreshAsync(forceRefresh: true, cancellationToken);
    }

    private async Task LoadCachedStateAndRefreshAsync(bool forceRefresh, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var user = await TryGetLoggedUserAsync(cancellationToken);
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
                cancellationToken.ThrowIfCancellationRequested();
                await InvokeAsync(StateHasChanged);
                _ = RefreshSnapshotAsync(user, cancellationToken);
                return;
            }
        }

        await RefreshSnapshotAsync(user, cancellationToken);
    }

    private async Task<UserSession?> TryGetLoggedUserAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ErrorMessage = string.Empty;

        try
        {
            return await LoginService.GetLoggedUser();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            Logger.LogError(ex, "Error while getting logged user for nav menu");
            return null;
        }
    }

    private async Task RefreshSnapshotAsync(UserSession user, CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var snapshot = await NavMenuStateCacheService.RefreshAsync(user);
            if (_currentUserId != user.UserId)
                return;

            cancellationToken.ThrowIfCancellationRequested();
            ApplySnapshot(snapshot);
            ErrorMessage = string.Empty;
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            Logger.LogError(ex, "Error while refreshing nav menu snapshot");
        }

        if (!cancellationToken.IsCancellationRequested)
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
        _disposeTokenSource.Cancel();
        AccountDataSynchronizationService.AccountsChanged -= AccountDataSynchronizationService_AccountsChanged;
        LoginService.LogginStateChanged -= LoginService_LogginStateChanged;
        _disposeTokenSource.Dispose();
    }
}