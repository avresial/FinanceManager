namespace FinanceManager.Domain.Entities.MoneyFlowModels;

public class InvestmentRate
{
    public DateTime Start { get; set; }
    public DateTime End { get; set; }

    public decimal Salary { get; set; }
    public decimal InvestmentsChange { get; set; }

    public decimal GetPercentage() => InvestmentsChange / Salary;
}
