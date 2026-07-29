using FinanceManager.Domain.MoneyFlow.Entities;

namespace FinanceManager.Components.Features.FinancialAccounts.Models;

public sealed record InvestmentAccountChartModel(
    string SelectedRange,
    DateTime StartDate,
    DateTime EndDate,
    List<TimeSeriesModel> Series,
    List<TimeSeriesModel> BenchmarkSeries,
    string BenchmarkName,
    decimal CurrentBalance,
    decimal CapitalValue,
    decimal CurrentValue,
    decimal BalanceChange,
    decimal? BalanceChangePercent,
    List<InvestmentHoldingModel> Holdings);