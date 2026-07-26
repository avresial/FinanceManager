using FinanceManager.Components.Features.Dashboard.Models;
using FinanceManager.Components.Shared.Services;
using FinanceManager.Domain.Identity.Services;
using Microsoft.Extensions.Logging;

namespace FinanceManager.Components.Features.Dashboard.Services;

/// <summary>
/// Holds the logged-in user's dashboard card show/hide preferences, backed by
/// <see cref="ISnapshotService"/> (browser local storage). Preferences are loaded once per
/// circuit and written through on every change, so <see cref="IsHidden"/> can be queried
/// synchronously while the dashboard renders.
/// </summary>
public class DashboardCardVisibilityService(
    ISnapshotService snapshotService,
    ILoginService loginService,
    ILogger<DashboardCardVisibilityService> logger)
{
    private readonly HashSet<string> _hiddenCardIds = [];
    private string? _key;
    private bool _loaded;

    /// <summary>Loads the persisted preferences once; safe to call repeatedly.</summary>
    public async Task EnsureLoadedAsync()
    {
        if (_loaded)
            return;

        try
        {
            var key = await GetKeyAsync();
            var snapshot = await snapshotService.GetAsync<DashboardCardVisibilitySnapshot>(key);
            if (snapshot is not null)
            {
                _hiddenCardIds.Clear();
                _hiddenCardIds.UnionWith(snapshot.HiddenCardIds);
            }
        }
        catch (Exception ex)
        {
            // A failure to read preferences must not break the dashboard; fall back to "all visible".
            logger.LogWarning(ex, "Failed to load dashboard card visibility preferences.");
        }
        finally
        {
            _loaded = true;
        }
    }

    /// <summary>True when the user has explicitly hidden the card with <paramref name="cardId"/>.</summary>
    public bool IsHidden(string cardId) => _hiddenCardIds.Contains(cardId);

    /// <summary>Records and persists whether the card with <paramref name="cardId"/> is hidden.</summary>
    public async Task SetHiddenAsync(string cardId, bool hidden)
    {
        await EnsureLoadedAsync();

        var changed = hidden ? _hiddenCardIds.Add(cardId) : _hiddenCardIds.Remove(cardId);
        if (!changed)
            return;

        try
        {
            var key = await GetKeyAsync();
            await snapshotService.SetAsync(key, new DashboardCardVisibilitySnapshot { HiddenCardIds = [.. _hiddenCardIds] });
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to persist dashboard card visibility preferences.");
        }
    }

    // Per-user key so each account keeps its own preferences within the same browser.
    private async Task<string> GetKeyAsync()
    {
        if (_key is not null)
            return _key;

        var user = await loginService.GetLoggedUser();
        _key = $"dashboard-card-visibility:{user?.UserId ?? 0}";
        return _key;
    }
}