using FinanceManager.Domain.FinancialAccounts.Shared.Entities;
using FinanceManager.Domain.Identity.Entities;
using System.Text.Json.Serialization;

namespace FinanceManager.Domain.FinancialAccounts.Bond.Entities;

public class BondAccountEntry(int accountId, long entryId, DateTime postingDate, decimal value, decimal valueChange, int bondDetailsId) : ILedgerEntry
{
    [JsonPropertyOrder(-2)]
    public int BondDetailsId { get; set; } = bondDetailsId;

    [JsonInclude]
    [JsonPropertyOrder(1)]
    public long EntryId { get; internal set; } = entryId;

    [JsonPropertyOrder(3)]
    public DateTime PostingDate { get; set; } = postingDate;

    private decimal _value = value;

    [JsonPropertyOrder(4)]
    public decimal Value
    {
        get => _value;
        set => _value = value;
    }

    [JsonPropertyOrder(-1)]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyOrder(6)]
    public ICollection<FinancialLabel> Labels { get; set; } = [];

    [JsonPropertyOrder(2)]
    public int AccountId { get; set; } = accountId;

    [JsonPropertyOrder(5)]
    public decimal ValueChange { get; set; } = valueChange;

    public void Update(BondAccountEntry entry)
    {
        if (EntryId != entry.EntryId) throw new Exception("Entry id can not be changed.");

        PostingDate = entry.PostingDate;
        Description = entry.Description;
        var valueChangeChange = entry.ValueChange - ValueChange;
        Value += valueChangeChange;
        ValueChange = entry.ValueChange;
        AccountId = entry.AccountId;
        Labels = entry.Labels ?? [];
        BondDetailsId = entry.BondDetailsId;
    }

    public BondAccountEntry GetCopy() => new(AccountId, EntryId, PostingDate, Value, ValueChange, BondDetailsId)
    {
        Description = Description,
        Labels = [.. Labels],
    };

    public decimal GetPriceAt(DateOnly date, BondDetails bondDetails)
    {
        if (date < DateOnly.FromDateTime(PostingDate))
            return 0;

        var priceHistory = GetPrice(date, bondDetails);
        return priceHistory.TryGetValue(date, out var price) ? price : 0;
    }

    public Dictionary<DateOnly, decimal> GetPrice(DateOnly date, BondDetails bondDetails)
    {
        if (bondDetails.CalculationMethods.Count == 0) throw new InvalidOperationException($"{nameof(BondDetails.CalculationMethods)} can't be empty");

        var capitalization = BondDetails.Capitalization;
        switch (capitalization)
        {
            case Capitalization.Annual:
                Dictionary<DateOnly, decimal> result = [];
                decimal capital = Value;
                decimal current = capital;
                var postingDate = DateOnly.FromDateTime(PostingDate);
                result[postingDate] = current * bondDetails.UnitValue;
                for (var i = postingDate.AddDays(1); i <= date; i = i.AddDays(1))
                {
                    var calculation = bondDetails.CalculationMethods.FirstOrDefault(cm => cm.IsActiveAt(i));
                    if (calculation is null) continue;
                    decimal change = Math.Round(capital * calculation.Rate / 365, 5);
                    current += change;

                    if ((i.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc) - postingDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc)).Days % 365 == 0)
                        capital = current;
                    result[i] = current * bondDetails.UnitValue;
                }
                return result;

            default: throw new NotImplementedException($"Capitalization method {capitalization} is not implemented.");
        }
    }

    public override string ToString() =>
    $"PostingDate: {PostingDate}, EntryId: {EntryId}, Value: {Value}, ValueChange: {ValueChange}, BondDetailsId: {BondDetailsId}, Description: {Description}";
}