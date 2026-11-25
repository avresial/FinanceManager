namespace FinanceManager.Contracts.Labels;

public class FinancialLabelDto
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public ICollection<FinancialLabelClassificationDto> Classifications { get; set; } = [];
}