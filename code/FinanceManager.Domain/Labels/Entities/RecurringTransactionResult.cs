namespace FinanceManager.Domain.Labels.Entities;

public record RecurringTransactionEntryReference(int AccountId, int EntryId);

public class RecurringTransactionResult(string name, decimal value)
{
    public Guid PatternId { get; set; }
    public string Name { get; set; } = name;
    public decimal Value { get; set; } = value;
    public RecurringCadence Cadence { get; set; } = RecurringCadence.Monthly;
    public DateTime LastChargeDate { get; set; }
    public DateTime NextExpectedChargeDate { get; set; }
    public decimal LastAmount { get; set; }
    public decimal PreviousAmount { get; set; }
    public decimal PriceDelta => LastAmount - PreviousAmount;
    public decimal MonthlyCost { get; set; }
    public decimal AnnualCost { get; set; }
    public bool IsMuted { get; set; }
    public bool IsCancelled { get; set; }
    public bool IsFlaggedForReview { get; set; }
    public List<RecurringTransactionEntryReference> Entries { get; set; } = [];

    // Required for JSON deserialization.
    public RecurringTransactionResult() : this(string.Empty, 0m) { }
}