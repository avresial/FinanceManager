namespace FinanceManager.Domain.MoneyFlow.Entities;

public record UnrealizedGainLossAccountResult(
    int AccountId,
    string AccountName,
    decimal CostBasis,
    decimal CurrentValue,
    decimal UnrealizedGainLoss,
    decimal UnrealizedGainLossPercent,
    DateTime AsOfDate,
    int ExcludedInstrumentsCount
);