namespace FinanceManager.Domain.Entities.MoneyFlowModels;

public record RecurringTransactionEntryReference(int AccountId, int EntryId);

public class RecurringTransactionResult(string name, decimal value)
{
    public string Name { get; set; } = name;
    public decimal Value { get; set; } = value;
    public List<RecurringTransactionEntryReference> Entries { get; set; } = [];

    // Required for JSON deserialization.
    public RecurringTransactionResult() : this(string.Empty, 0m) { }
}