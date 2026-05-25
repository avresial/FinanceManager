namespace FinanceManager.Domain.Entities.Shared.Accounts;

public class FinancialLabelClassification
{
    public int Id { get; set; }
    public int LabelId { get; set; }
    public required string Kind { get; set; }
    public required string Value { get; set; }
}