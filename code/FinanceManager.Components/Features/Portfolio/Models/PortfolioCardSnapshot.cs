using FinanceManager.Components.Shared.Models;

namespace FinanceManager.Components.Features.Portfolio.Models;

public sealed class PortfolioCardSnapshot<T> : SnapshotBase where T : class
{
    public required string Scope { get; init; }
    public DateTime Start { get; init; }
    public DateTime End { get; init; }
    public required T Model { get; init; }
}