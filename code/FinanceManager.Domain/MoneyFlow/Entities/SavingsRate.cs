namespace FinanceManager.Domain.MoneyFlow.Entities;

public class SavingsRate
{
    public DateTime Start { get; set; }
    public DateTime End { get; set; }

    public decimal Salary { get; set; }
    public decimal Spending { get; set; }
    public decimal Saved => Salary - Spending;

    /// <summary>A rate is undefined when no salary landed in the period.</summary>
    public bool HasRate => Salary != 0;

    public decimal? GetPercentage() => HasRate ? Saved / Salary : null;
}