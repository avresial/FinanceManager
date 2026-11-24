using FinanceManager.Components.Shared.Components.Charts;
using FinanceManager.Domain.MoneyFlow.Entities;
using System.Text.Json.Serialization;

namespace FinanceManager.Components.Features.Portfolio.Models;

public sealed record InvestmentsValueModel(IReadOnlyList<TimeSeriesModel> Series, IReadOnlyList<TimeSeriesOverlay> Overlays,
    decimal CurrentValue, decimal CostBasis, string Currency, string BenchmarkName)
{
    /// <summary>False after a full exit (or before the first trade): there is no cost basis to compare the value with.</summary>
    [JsonIgnore]
    public bool HasOpenPositions => CostBasis > 0m || CurrentValue > 0m;
}