namespace FinanceManager.Domain.MoneyFlow.Entities;

public class InvestmentRate
{
    public DateTime Start { get; set; }
    public DateTime End { get; set; }

    public decimal Salary { get; set; }
    public decimal InvestmentsChange { get; set; }

    /// <summary>
    /// A rate is only meaningful when a salary actually landed in the period. Without one there is no
    /// denominator to measure the investments against, so the rate is undefined rather than zero.
    /// </summary>
    public bool HasRate => Salary != 0;

    public decimal? GetPercentage() => HasRate ? InvestmentsChange / Salary : null;
}