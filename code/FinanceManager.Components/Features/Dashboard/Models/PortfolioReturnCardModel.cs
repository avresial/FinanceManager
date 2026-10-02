using FinanceManager.Domain.MoneyFlow.Entities;

namespace FinanceManager.Components.Features.Dashboard.Models;

public sealed record PortfolioReturnCardModel(
    decimal? AnnualizedReturn,
    MoneyWeightedReturnStatus? MoneyWeightedStatus,
    decimal? TotalReturn,
    TimeWeightedReturnStatus? TimeWeightedStatus,
    decimal? ExternalCashMovement)
{
    public static PortfolioReturnCardModel FromSource(PortfolioReturnSourceModel source) => new(
        source.MoneyWeightedReturn?.AnnualizedReturn, source.MoneyWeightedReturn?.Status,
        source.TimeWeightedReturn?.TotalReturn, source.TimeWeightedReturn?.Status,
        source.Attribution?.IsAvailable == true ? source.Attribution.ExternalCashMovement : null);
}