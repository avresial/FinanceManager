using FinanceManager.Components.Shared.Components.Charts;
using FinanceManager.Domain.MoneyFlow.Entities;

namespace FinanceManager.Components.Features.Portfolio.Models;

public sealed record InvestmentsValueModel(IReadOnlyList<TimeSeriesModel> Series, IReadOnlyList<TimeSeriesOverlay> Overlays,
    decimal CurrentValue, decimal CostBasis, string Currency, string BenchmarkName);