namespace FinanceManager.Domain.Assets.Services;

/// <summary>
/// Outcome of resolving an investment price, preserving a publication-window message when FX data
/// for the requested current UTC date is not available yet.
/// </summary>
public sealed record InvestmentPriceResult
{
    private InvestmentPriceResult(
        InvestmentPriceStatus status,
        decimal? price = null,
        string? message = null,
        DateTime? retryAtUtc = null)
    {
        Status = status;
        Price = price;
        Message = message;
        RetryAtUtc = retryAtUtc;
    }

    public InvestmentPriceStatus Status { get; }

    public decimal? Price { get; }

    public string? Message { get; }

    public DateTime? RetryAtUtc { get; }

    public static InvestmentPriceResult Success(decimal price) =>
        new(InvestmentPriceStatus.Success, price);

    public static InvestmentPriceResult NotFound() =>
        new(InvestmentPriceStatus.NotFound);

    public static InvestmentPriceResult NotYetPublished(string message, DateTime retryAtUtc) =>
        new(InvestmentPriceStatus.NotYetPublished, message: message, retryAtUtc: retryAtUtc);
}