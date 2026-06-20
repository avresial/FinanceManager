namespace FinanceManager.Domain.FinancialAccounts.Investments.Entities;

/// <summary>
/// Represents a minimal user investment transaction. This first version supports buy/sell only;
/// dividends, splits, transfers and tax records are intentionally deferred to later work.
/// Each transaction references the exact <see cref="AssetListing"/> traded (not just the asset).
/// </summary>
public class InvestmentTransaction
{
    /// <summary>Internal database identifier.</summary>
    public long Id { get; set; }

    /// <summary>User who owns this transaction.</summary>
    public long UserId { get; set; }

    /// <summary>
    /// Financial account this transaction belongs to. Added on top of the issue's minimal model so
    /// the existing account-based architecture (dashboards, net worth, money flow) keeps working.
    /// </summary>
    public int AccountId { get; set; }

    /// <summary>Listing that was traded, e.g. CSPX on the London Stock Exchange in USD.</summary>
    public long AssetListingId { get; set; }

    /// <summary>Listing that was traded.</summary>
    public AssetListing AssetListing { get; set; } = null!;

    /// <summary>Transaction direction (Buy or Sell).</summary>
    public InvestmentTransactionType Type { get; set; }

    /// <summary>Number of units/shares bought or sold (always positive), e.g. 3.25.</summary>
    public decimal Quantity { get; set; }

    /// <summary>Price per one unit/share, e.g. 520.45.</summary>
    public decimal UnitPrice { get; set; }

    /// <summary>Currency of the unit price (usually the listing's trading currency), e.g. "USD", "GBX".</summary>
    public string Currency { get; set; } = null!;

    /// <summary>Date when the transaction happened.</summary>
    public DateOnly TradeDate { get; set; }

    /// <summary>Optional broker fee. Retained because fees affect the real cost basis.</summary>
    public decimal? Fee { get; set; }

    /// <summary>Optional note entered by the user.</summary>
    public string? Notes { get; set; }

    /// <summary>Timestamp when the record was created in this system.</summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>Timestamp when the record was last updated in this system.</summary>
    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>Signed quantity delta this transaction contributes to a holding (+ for Buy, - for Sell).</summary>
    public decimal SignedQuantity => Type == InvestmentTransactionType.Sell ? -Quantity : Quantity;
}