using FinanceManager.Components.Shared.Components.Charts;
using FinanceManager.Domain.MoneyFlow.Entities;
using System.Text.Json.Serialization;

namespace FinanceManager.Components.Features.Portfolio.Models;

/// <param name="OpenPositionCount">Holdings with non-zero signed units (oversold ones included), exactly what the Holdings card lists.</param>
/// <param name="ExcludedPositionCount">Open positions left out of <paramref name="CurrentValue"/> and <paramref name="CostBasis"/> for lack of a price or FX rate.</param>
public sealed record InvestmentsValueModel(IReadOnlyList<TimeSeriesModel> Series, IReadOnlyList<TimeSeriesOverlay> Overlays,
    decimal CurrentValue, decimal CostBasis, string Currency, string BenchmarkName, int OpenPositionCount = 0, int ExcludedPositionCount = 0)
{
    /// <summary>Position presence, independent of pricing: false only after a full exit or before the first trade.</summary>
    [JsonIgnore]
    public bool HasOpenPositions => OpenPositionCount > 0;

    /// <summary>True when at least one long position is priced and convertible, so the cost basis can be compared with the value.</summary>
    [JsonIgnore]
    public bool HasPricedPositions => CostBasis > 0m;
}