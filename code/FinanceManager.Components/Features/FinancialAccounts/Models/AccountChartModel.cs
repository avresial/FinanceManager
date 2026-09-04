using FinanceManager.Domain.MoneyFlow.Entities;

namespace FinanceManager.Components.Features.FinancialAccounts.Models;

public sealed record AccountChartModel(
    string SelectedRange,
    DateTime StartDate,
    DateTime EndDate,
    List<TimeSeriesModel> Series,
    decimal CurrentBalance,
    decimal BalanceChange,
    decimal? BalanceChangePercent,
    List<TimeSeriesModel>? CapitalSeries = null);