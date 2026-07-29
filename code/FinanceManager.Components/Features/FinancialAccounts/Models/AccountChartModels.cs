using FinanceManager.Domain.MoneyFlow.Entities;

namespace FinanceManager.Components.Features.FinancialAccounts.Models;

public sealed record AccountChartModel(
    string SelectedRange,
    DateTime StartDate,
    DateTime EndDate,
    List<TimeSeriesModel> Series,
    decimal CurrentBalance,
    decimal BalanceChange,
    decimal? BalanceChangePercent);

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

public sealed record InvestmentHoldingModel(
    long ListingId,
    string Ticker,
    string ExchangeName,
    string Currency,
    decimal Quantity,
    decimal LatestPrice,
    decimal Value);