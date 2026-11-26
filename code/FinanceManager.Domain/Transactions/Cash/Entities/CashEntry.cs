using FinanceManager.Domain.Labels.Entities;
using FinanceManager.Domain.Transactions.Shared;
using System.Text.Json.Serialization;

namespace FinanceManager.Domain.Transactions.Cash.Entities;

public class CashEntry(long entryId, DateTime postingDate, decimal valueChange) : ILedgerEntry
{
    public string? ContractorDetails { get; set; }

    [JsonInclude]
    [JsonPropertyOrder(1)]
    public long EntryId { get; internal set; } = entryId;

    [JsonPropertyOrder(3)]
    public DateTime PostingDate { get; set; } = postingDate;

    [JsonPropertyOrder(-1)]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyOrder(6)]
    public ICollection<FinancialLabel> Labels { get; set; } = [];

    [JsonIgnore]
    public int UserId { get; set; }

    [JsonIgnore]
    public int CurrencyId { get; set; }

    [JsonPropertyOrder(5)]
    public decimal ValueChange { get; set; } = valueChange;

    public void Update(CashEntry entry)
    {
        if (EntryId != entry.EntryId) throw new Exception("Entry id can not be changed.");

        PostingDate = entry.PostingDate;
        Description = entry.Description;
        ValueChange = entry.ValueChange;
        Labels = entry.Labels ?? [];
        ContractorDetails = entry.ContractorDetails;
    }

    public CashEntry GetCopy() => new(EntryId, PostingDate, ValueChange)
    {
        UserId = UserId,
        CurrencyId = CurrencyId,
        Description = this.Description,
        ContractorDetails = this.ContractorDetails,
    };

    public override string ToString() => $"PostingDate: {PostingDate}, EntryId: {EntryId}, ValueChange: {ValueChange}";
}