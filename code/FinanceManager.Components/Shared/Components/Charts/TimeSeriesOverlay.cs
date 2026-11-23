using FinanceManager.Domain.MoneyFlow.Entities;

namespace FinanceManager.Components.Shared.Components.Charts;

public sealed record TimeSeriesOverlay(string Name, IReadOnlyList<TimeSeriesModel> Data, string Color);