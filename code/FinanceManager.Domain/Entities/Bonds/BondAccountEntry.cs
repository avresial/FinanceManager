using FinanceManager.Domain.Entities.Shared.Accounts;
using FinanceManager.Domain.Enums;

namespace FinanceManager.Domain.Entities.Bonds;

public class BondAccountEntry(int accountId, int entryId, DateTime postingDate, decimal value, decimal valueChange, int bondDetailsId) : FinancialEntryBase(accountId, entryId, postingDate, value, valueChange)
{
    public int BondDetailsId { get; set; } = bondDetailsId;

    public override void Update(FinancialEntryBase entry)
    {
        base.Update(entry);

        if (entry is BondAccountEntry bondEntry)
        {
            BondDetailsId = bondEntry.BondDetailsId;
        }
    }

    public BondAccountEntry GetCopy() => new(AccountId, EntryId, PostingDate, Value, ValueChange, BondDetailsId)
    {
        Labels = this.Labels,
    };

    public Dictionary<DateOnly, decimal> GetPrice(DateOnly date, BondDetails bondDetails)
    {
        var capitalization = BondDetails.Capitalization;
        switch (capitalization)
        {
            case Capitalization.Annual:
                Dictionary<DateOnly, decimal> result = new();
                decimal capital = Value;
                decimal current = capital;
                var postingDate = DateOnly.FromDateTime(PostingDate);
                for (var i = postingDate; i < date; i = i.AddDays(1))
                {
                    var calculation = bondDetails.CalculationMethods.FirstOrDefault(cm => cm.IsActiveAt(date));
                    if (calculation is null) continue;
                    decimal change = capital * calculation.Rate / 365;
                    current += change;

                    if ((i.ToDateTime(TimeOnly.MinValue) - postingDate.ToDateTime(TimeOnly.MinValue)).Days % 365 == 0)
                        capital = current;
                    result[i] = current;
                }
                return result;

            default: throw new NotImplementedException($"Capitalization method {capitalization} is not implemented.");
        }
    }

    public override string ToString() => $"PostingDate: {PostingDate}, EntryId: {EntryId}, Value: {Value}, ValueChange: {ValueChange}, BondDetailsId: {BondDetailsId}";
}