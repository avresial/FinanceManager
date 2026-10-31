using FinanceManager.Domain.Assets.Entities;

namespace FinanceManager.Domain.FinancialAccounts.Investments.Entities;

/// <summary>Historical cash income recorded independently of trades, balances and valuation.</summary>
public class DividendPayment
{
    public long Id { get; set; }
    public int UserId { get; set; }
    public int AccountId { get; set; }
    public long AssetListingId { get; set; }
    public AssetListing AssetListing { get; set; } = null!;
    public DateOnly PaymentDate { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}