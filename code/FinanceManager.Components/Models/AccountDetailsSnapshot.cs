using FinanceManager.Domain.FinancialAccounts.Shared.Entities;

namespace FinanceManager.Components.Models;

/// <summary>
/// Persisted snapshot of an account's initial transaction history so the account
/// details page can paint its last-rendered entries instantly on re-navigation,
/// then reconcile against a fresh API response. Chart data is never snapshotted —
/// it always loads from the API.
/// </summary>
/// <remarks>
/// Only the entries and the identity needed to rebuild the account are stored. The
/// account entity itself is not serialized directly because its JSON constructor binds
/// to fields the serializer cannot round-trip; the owning component rebuilds the account
/// from these values, mirroring how the typed HTTP client maps the API DTO.
/// </remarks>
/// <typeparam name="TEntry">The concrete entry type (e.g. CurrencyAccountEntry).</typeparam>
public sealed class AccountDetailsSnapshot<TEntry> : SnapshotBase
{
    public int UserId { get; set; }
    public int AccountId { get; set; }
    public string Name { get; set; } = string.Empty;

    /// <summary>Account label, used to rebuild currency and bond accounts; ignored for stock.</summary>
    public AccountLabel AccountType { get; set; }

    public List<TEntry> Entries { get; set; } = [];
}