namespace FinanceManager.Components.Features.Dashboard.Models;

public sealed record InvestmentRateMonthModel(DateOnly Month, decimal Salary, decimal InvestmentsChange);