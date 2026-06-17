namespace FinanceManager.Domain.MoneyFlow.Entities;

public record UnrealizedGainLossInstrumentResult(
    int AccountId,
    string AccountName,
    string InstrumentId,
    string InstrumentName,
    decimal Quantity,
    decimal CostBasis,
    decimal CurrentValue,
    decimal UnrealizedGainLoss,
    decimal UnrealizedGainLossPercent,
    DateTime AsOfDate,
    bool IsExcludedFromTotals,
    string? WarningMessage
);