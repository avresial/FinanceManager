using FinanceManager.Domain.MoneyFlow.Entities;

namespace FinanceManager.Components.Features.Dashboard.Models;

public sealed record PortfolioReturnSourceModel(
    MoneyWeightedReturnResult? MoneyWeightedReturn,
    TimeWeightedReturnResult? TimeWeightedReturn,
    PortfolioReturnAttributionResult? Attribution);