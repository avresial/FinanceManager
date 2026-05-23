namespace FinanceManager.Domain.Entities.MoneyFlowModels;

public record RecurringTransactionEntryReference(int AccountId, int EntryId);

public class RecurringTransactionResult
{
    public string Name { get; set; } = string.Empty;
    public decimal Value { get; set; }
    public List<RecurringTransactionEntryReference> Entries { get; set; } = [];

    public RecurringTransactionResult() { }

    public RecurringTransactionResult(string name, decimal value)
    {
        Name = name;
        Value = value;
    }
}
