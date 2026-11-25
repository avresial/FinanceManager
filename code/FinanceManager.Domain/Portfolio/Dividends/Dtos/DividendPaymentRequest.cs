namespace FinanceManager.Domain.Portfolio.Dividends.Dtos;

public class DividendPaymentRequest
{
    public long AssetListingId { get; set; }
    public DateOnly PaymentDate { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = string.Empty;
    public string? Notes { get; set; }
}