using FinanceManager.Domain.FinancialAccounts.Shared.Entities;
using System.Text.Json.Serialization;

namespace FinanceManager.Domain.FinancialAccounts.Currencies.Entities;

public class CurrencyAccountEntry(int? accountId, long entryId, DateTime postingDate, decimal value, decimal valueChange) : ILedgerEntry
{
    public string? ContractorDetails { get; set; }

    [JsonInclude]
    [JsonPropertyOrder(1)]
    public long EntryId { get; internal set; } = entryId;

    [JsonPropertyOrder(3)]
    public DateTime PostingDate { get; set; } = postingDate;

    private decimal _value = value;

    // Dormant running balance, dropped in #1144.
    [JsonIgnore]
    public decimal Value
    {
        get => _value;
        set => _value = value;
    }

    [JsonPropertyOrder(-1)]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyOrder(6)]
    public ICollection<FinancialLabel> Labels { get; set; } = [];

    [JsonIgnore]
    public int UserId { get; set; }

    [JsonIgnore]
    public int CurrencyId { get; set; }

    // Dormant account link, dropped in #1144.
    [JsonIgnore]
    public int? AccountId { get; set; } = accountId;

    [JsonPropertyOrder(5)]
    public decimal ValueChange { get; set; } = valueChange;

    public void Update(CurrencyAccountEntry entry)
    {
        if (EntryId != entry.EntryId) throw new Exception("Entry id can not be changed.");

        PostingDate = entry.PostingDate;
        Description = entry.Description;
        ValueChange = entry.ValueChange;
        Labels = entry.Labels ?? [];
        ContractorDetails = entry.ContractorDetails;
    }

    public CurrencyAccountEntry GetCopy() => new(null, EntryId, PostingDate, 0, ValueChange)
    {
        UserId = UserId,
        CurrencyId = CurrencyId,
        Description = this.Description,
        ContractorDetails = this.ContractorDetails,
    };

    public override string ToString() => $"PostingDate: {PostingDate}, EntryId: {EntryId}, ValueChange: {ValueChange}";
}