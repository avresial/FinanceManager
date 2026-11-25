using FinanceManager.Domain.FinancialAccounts.Shared.Entities;
using FinanceManager.Domain.Identity.Entities;
using System.Text.Json.Serialization;

namespace FinanceManager.Domain.FinancialAccounts.Bond.Entities;

public class BondAccountEntry(int? accountId, long entryId, DateTime postingDate, decimal value, decimal valueChange, int bondDetailsId) : ILedgerEntry
{
    [JsonPropertyOrder(-2)]
    public int BondDetailsId { get; set; } = bondDetailsId;

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

    // Dormant account link, dropped in #1144.
    [JsonIgnore]
    public int? AccountId { get; set; } = accountId;

    [JsonPropertyOrder(5)]
    public decimal ValueChange { get; set; } = valueChange;

    public void Update(BondAccountEntry entry)
    {
        if (EntryId != entry.EntryId) throw new Exception("Entry id can not be changed.");

        PostingDate = entry.PostingDate;
        Description = entry.Description;
        ValueChange = entry.ValueChange;
        Labels = entry.Labels ?? [];
        BondDetailsId = entry.BondDetailsId;
    }

    public BondAccountEntry GetCopy() => new(null, EntryId, PostingDate, 0, ValueChange, BondDetailsId)
    {
        UserId = UserId,
        Description = Description,
        Labels = [.. Labels],
    };

    /// <summary>
    /// Daily value of <paramref name="units"/> accruing from <paramref name="anchorDate"/> through <paramref name="throughDate"/>.
    /// Shared by single entries and by the owner-pooled positions, where the anchor is the latest pooled entry's posting date.
    /// </summary>
    public static Dictionary<DateOnly, decimal> GetPrice(DateOnly anchorDate, decimal units, DateOnly throughDate, BondDetails bondDetails)
    {
        if (bondDetails.CalculationMethods.Count == 0) throw new InvalidOperationException($"{nameof(BondDetails.CalculationMethods)} can't be empty");

        var capitalization = BondDetails.Capitalization;
        switch (capitalization)
        {
            case Capitalization.Annual:
                Dictionary<DateOnly, decimal> result = [];
                decimal capital = units;
                decimal current = capital;
                result[anchorDate] = current * bondDetails.UnitValue;
                for (var i = anchorDate.AddDays(1); i <= throughDate; i = i.AddDays(1))
                {
                    var calculation = bondDetails.CalculationMethods.FirstOrDefault(cm => cm.IsActiveAt(i));
                    if (calculation is null) continue;
                    decimal change = Math.Round(capital * calculation.Rate / 365, 5);
                    current += change;

                    if ((i.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc) - anchorDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc)).Days % 365 == 0)
                        capital = current;
                    result[i] = current * bondDetails.UnitValue;
                }
                return result;

            default: throw new NotImplementedException($"Capitalization method {capitalization} is not implemented.");
        }
    }

    public override string ToString() =>
    $"PostingDate: {PostingDate}, EntryId: {EntryId}, ValueChange: {ValueChange}, BondDetailsId: {BondDetailsId}, Description: {Description}";
}