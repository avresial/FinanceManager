using FinanceManager.Domain.MoneyFlow.Entities;

namespace FinanceManager.Components.Features.Dashboard.Models;

public sealed record PortfolioReturnAttributionCardModel(
    PortfolioReturnAttributionStatus Status,
    decimal? TotalChange,
    decimal? ExternalCashMovement,
    decimal? MarketEffect,
    decimal? FeeEffect,
    decimal? FxEffect,
    decimal? ReconciliationDifference,
    List<string> UnsupportedComponents)
{
    public bool IsAvailable => Status == PortfolioReturnAttributionStatus.Available
        && TotalChange.HasValue && ExternalCashMovement.HasValue && MarketEffect.HasValue
        && FeeEffect.HasValue && FxEffect.HasValue && ReconciliationDifference.HasValue;

    public static PortfolioReturnAttributionCardModel FromResult(PortfolioReturnAttributionResult result) => new(
        result.Status, result.TotalChange, result.ExternalCashMovement, result.MarketEffect,
        result.FeeEffect, result.FxEffect, result.ReconciliationDifference,
        [.. result.UnsupportedComponents.Select(component => component.Name)]);
}