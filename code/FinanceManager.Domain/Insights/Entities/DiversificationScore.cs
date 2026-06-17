namespace FinanceManager.Domain.Insights.Entities;

public record DiversificationScore(int Score, int AssetClassScore, int HoldingsScore, string Band);