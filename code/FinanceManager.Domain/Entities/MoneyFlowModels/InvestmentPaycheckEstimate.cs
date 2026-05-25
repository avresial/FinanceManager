namespace FinanceManager.Domain.Entities.MoneyFlowModels;

public class InvestmentPaycheckEstimate
{
    public DateTime AsOfDate { get; set; }
    public decimal AnnualWithdrawalRate { get; set; }
    public decimal InvestableAssetsValue { get; set; }
    public decimal SustainableMonthlyPaycheck { get; set; }

    public int SalaryMonthsRequested { get; set; }
    public int SalaryMonthsUsed { get; set; }
    public decimal? AverageMonthlySalary { get; set; }
    public decimal? IncomeReplacementRatio { get; set; }

    public bool HasSalaryData => SalaryMonthsUsed > 0 && AverageMonthlySalary.HasValue;
    public bool HasPartialSalaryHistory => SalaryMonthsUsed > 0 && SalaryMonthsUsed < SalaryMonthsRequested;
}