namespace FinanceManager.Domain.FinancialAccounts.Shared.Entities;

public class FinancialLabel
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public ICollection<FinancialLabelClassification> Classifications { get; set; } = [];
}