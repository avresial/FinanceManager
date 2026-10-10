using FinanceManager.Components.Features.Dashboard.Models;
using FinanceManager.Components.Shared.Services;
using FinanceManager.Domain.Identity.Services;
using Microsoft.Extensions.Logging;

namespace FinanceManager.Components.Features.Dashboard.Services;

/// <summary>
/// Holds the logged-in user's page card show/hide preferences, backed by
/// <see cref="ISnapshotService"/> (browser local storage). Preferences are loaded once per
/// circuit and written through on every change, so <see cref="IsHidden"/> can be queried
/// synchronously while the card grid renders.
/// </summary>
public class DashboardCardVisibilityService(
    ISnapshotService snapshotService,
    ILoginService loginService,
    ILogger<DashboardCardVisibilityService> logger)
{
    private readonly Dictionary<string, HashSet<string>> _hiddenByPage = [];
    private readonly HashSet<string> _loadedPages = [];

    /// <summary>Loads the persisted preferences once per page; safe to call repeatedly.</summary>
    public async Task EnsureLoadedAsync(string page = "dashboard")
    {
        if (_loadedPages.Contains(page))
            return;

        try
        {
            var key = await GetKeyAsync(page);
            var snapshot = await snapshotService.GetAsync<DashboardCardVisibilitySnapshot>(key);
            if (snapshot is not null)
            {
                GetHiddenCards(page).UnionWith(snapshot.HiddenCardIds);
            }
        }
        catch (Exception ex)
        {
            // A failure to read preferences must not break the dashboard; fall back to "all visible".
            logger.LogWarning(ex, "Failed to load dashboard card visibility preferences.");
        }
        finally
        {
            _loadedPages.Add(page);
        }
    }

    /// <summary>True when the user has explicitly hidden the card with <paramref name="cardId"/>.</summary>
    public bool IsHidden(string cardId, string page = "dashboard") => GetHiddenCards(page).Contains(cardId);

    /// <summary>Records and persists whether the card with <paramref name="cardId"/> is hidden.</summary>
    public async Task SetHiddenAsync(string cardId, bool hidden, string page = "dashboard")
    {
        await EnsureLoadedAsync(page);

        var hiddenCards = GetHiddenCards(page);
        var changed = hidden ? hiddenCards.Add(cardId) : hiddenCards.Remove(cardId);
        if (!changed)
            return;

        try
        {
            var key = await GetKeyAsync(page);
            await snapshotService.SetAsync(key, new DashboardCardVisibilitySnapshot { HiddenCardIds = [.. hiddenCards] });
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to persist dashboard card visibility preferences.");
        }
    }

    private HashSet<string> GetHiddenCards(string page)
    {
        if (!_hiddenByPage.TryGetValue(page, out var cards))
            _hiddenByPage[page] = cards = [];
        return cards;
    }

    // Keep the original Dashboard key so existing preferences survive this extraction.
    private async Task<string> GetKeyAsync(string page)
    {
        var user = await loginService.GetLoggedUser();
        return page == "dashboard"
            ? $"dashboard-card-visibility:{user?.UserId ?? 0}"
            : $"dashboard-card-visibility:{page}:{user?.UserId ?? 0}";
    }
}