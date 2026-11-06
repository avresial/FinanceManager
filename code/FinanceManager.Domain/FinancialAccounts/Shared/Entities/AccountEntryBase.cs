using System.Text.Json.Serialization;

namespace FinanceManager.Domain.FinancialAccounts.Shared.Entities;

/// <summary>
/// Minimal common parent of every per-account transaction entity (currency, bond and investment entries).
/// It only holds the identity members shared by all of them.
/// <para>
/// Entries are serialized directly by some controllers. System.Text.Json writes derived-type members first and
/// base-type members last, so the explicit <see cref="JsonPropertyOrderAttribute"/> values on this type and on
/// <see cref="FinancialEntryBase"/> keep the pre-refactor payload order (derived members, then EntryId, AccountId,
/// PostingDate, Value, ValueChange, Labels).
/// </para>
/// </summary>
public abstract class AccountEntryBase
{
    protected AccountEntryBase()
    {
    }

    protected AccountEntryBase(int accountId, long entryId)
    {
        AccountId = accountId;
        EntryId = entryId;
    }

    /// <summary>
    /// Entry identifier. Assigned by the persistence layer, so the setter stays restricted to the Domain assembly
    /// (EF Core and the entry repositories set it through reflection, which ignores accessibility).
    /// </summary>
    [JsonPropertyOrder(1)]
    public long EntryId { get; internal set; }

    /// <summary>Financial account this entry belongs to.</summary>
    [JsonPropertyOrder(2)]
    public int AccountId { get; set; }
}