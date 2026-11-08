using System.Text.Json.Serialization;

namespace FinanceManager.Domain.FinancialAccounts.Shared.Entities;

/// <summary>
/// Minimal common parent of every per-account transaction entity (currency, bond and investment entries).
/// It holds the identity, posting date, description and signed value change shared by all of them.
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

    protected AccountEntryBase(int accountId, long entryId, DateTime postingDate, decimal value = 0m)
    {
        PostingDate = postingDate;
        AccountId = accountId;
        EntryId = entryId;
        _value = value;
    }

    // Backing field instead of an auto-property so the constructor does not call the virtual setter.
    private decimal _value;

    /// <summary>
    /// Entry identifier. Assigned by the persistence layer, so the setter stays restricted to the Domain assembly
    /// (EF Core and the entry repositories set it through reflection, which ignores accessibility).
    /// </summary>
    [JsonInclude]
    [JsonPropertyOrder(1)]
    public long EntryId { get; internal set; }

    [JsonPropertyOrder(3)]
    public virtual DateTime PostingDate { get; set; }

    /// <summary>
    /// Running balance of the account units after this entry: currency amount, bond count, or for an investment
    /// transaction the units of its listing held in the account. Virtual so investment transactions can keep it
    /// out of their JSON, as no endpoint returns that entity.
    /// </summary>
    [JsonPropertyOrder(4)]
    public virtual decimal Value
    {
        get => _value;
        set => _value = value;
    }

    /// <summary>Signed change in the account units (currency amount, bond count or investment quantity).</summary>
    public abstract decimal ValueChange { get; set; }

    /// <summary>
    /// Free-text description of the entry; empty when the user entered none. Ordered before the derived types' own
    /// members (order 0) so currency payloads keep Description ahead of ContractorDetails.
    /// </summary>
    [JsonPropertyOrder(-1)]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyOrder(6)]
    public ICollection<FinancialLabel> Labels { get; set; } = [];

    /// <summary>Financial account this entry belongs to.</summary>
    [JsonPropertyOrder(2)]
    public int AccountId { get; set; }
}