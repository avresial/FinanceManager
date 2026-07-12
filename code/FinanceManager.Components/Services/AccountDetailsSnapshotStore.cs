using FinanceManager.Components.Models;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace FinanceManager.Components.Services;

public sealed class AccountDetailsSnapshotStore(
    ISnapshotService snapshots,
    ILogger<AccountDetailsSnapshotStore> logger)
{
    public async Task<AccountDetailsSnapshot<TEntry>?> GetAsync<TEntry>(int userId, int accountId)
    {
        try
        {
            var snapshot = await snapshots.GetAsync<AccountDetailsSnapshot<TEntry>>(Key(userId, accountId));
            return snapshot?.AccountId == accountId ? snapshot : null;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to read account details snapshot; continuing with fresh fetch.");
            return null;
        }
    }

    public async Task SaveIfChangedAsync<TEntry>(AccountDetailsSnapshot<TEntry> snapshot, AccountDetailsSnapshot<TEntry>? previous)
    {
        if (previous is not null && JsonSerializer.Serialize(snapshot.Entries) == JsonSerializer.Serialize(previous.Entries))
            return;

        try
        {
            await snapshots.SetAsync(Key(snapshot.UserId, snapshot.AccountId), snapshot);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Account details loaded but snapshot save failed.");
        }
    }

    private static string Key(int userId, int accountId) => $"account-details:{userId}:{accountId}";
}