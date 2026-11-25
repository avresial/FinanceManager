namespace FinanceManager.Domain.MoneyFlow.Entities;

public record UnrealizedGainLossResult(
    string AccountName,
    decimal CostBasis,
    decimal CurrentValue,
    decimal UnrealizedGainLoss,
    decimal UnrealizedGainLossPercent,
    DateTime AsOfDate,
    int ExcludedInstrumentsCount
);