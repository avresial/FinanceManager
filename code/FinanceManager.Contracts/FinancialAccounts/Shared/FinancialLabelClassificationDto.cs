namespace FinanceManager.Contracts.FinancialAccounts.Shared;

public class FinancialLabelClassificationDto
{
    public int Id { get; set; }
    public int LabelId { get; set; }
    public required string Kind { get; set; }
    public required string Value { get; set; }
}