namespace FinanceManager.Domain.Assets.Services;

/// <summary>
/// Outcome of resolving an investment price, preserving a publication-window message when FX data
/// for the requested current UTC date is not available yet.
/// </summary>
public closed record InvestmentPriceResult
{
    public sealed record Success(decimal Price) : InvestmentPriceResult;

    public sealed record NotFound : InvestmentPriceResult;

    public sealed record NotYetPublished(string Message, DateTime RetryAtUtc) : InvestmentPriceResult;
}