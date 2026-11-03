using FinanceManager.Domain.Shared;

namespace FinanceManager.Domain.FinancialAccounts.Shared.ValueObjects;

/// <summary>
/// The gain or loss of a position: what it is worth now against what it cost.
/// <see cref="Percent"/> is relative to the cost basis and is 0 when there is no cost basis.
/// </summary>
public readonly record struct GainLoss(decimal CostBasis, decimal CurrentValue)
{
    public decimal Amount => CurrentValue - CostBasis;

    public decimal Percent => Percentage.Of(Amount, CostBasis) ?? 0m;
}