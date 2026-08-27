namespace FinanceManager.Domain.Assets.Services;

/// <summary>Terminal state of an investment-price lookup.</summary>
public enum InvestmentPriceStatus
{
    Success,
    NotFound,
    NotYetPublished,
}