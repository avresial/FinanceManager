namespace FinanceManager.Domain.Entities.MoneyFlowModels;

public record DiversificationScore(int Score, int AssetClassScore, int HoldingsScore, string Band);