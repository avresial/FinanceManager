namespace FinanceManager.Domain.MoneyFlow.Entities;

public class InvestmentRate
{
    public DateTime Start { get; set; }
    public DateTime End { get; set; }

    public decimal Salary { get; set; }
    public decimal InvestmentsChange { get; set; }

    public decimal GetPercentage() => Salary == 0 ? 0 : InvestmentsChange / Salary;
}