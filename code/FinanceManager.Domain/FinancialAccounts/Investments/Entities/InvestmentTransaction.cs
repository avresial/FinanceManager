using FinanceManager.Domain.Assets.Entities;
using FinanceManager.Domain.FinancialAccounts.Shared.Entities;
using System.Text.Json.Serialization;

namespace FinanceManager.Domain.FinancialAccounts.Investments.Entities;

/// <summary>
/// Represents a minimal user investment transaction. This first version supports buy/sell only;
/// dividends, splits, transfers and tax records are intentionally deferred to later work.
/// Each transaction references the exact <see cref="AssetListing"/> traded (not just the asset).
/// </summary>
public class InvestmentTransaction
{
    public InvestmentTransaction() { }

    public InvestmentTransaction(long entryId) => EntryId = entryId;

    /// <summary>User who owns this transaction.</summary>
    public long UserId { get; set; }

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
    [JsonIgnore]
    public DateOnly TradeDate
    {
        get => DateOnly.FromDateTime(PostingDate);
        set => PostingDate = value.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
    }

    // Trades have a calendar date, not an instant: preserve its day even for Local/Unspecified inputs.
    [JsonPropertyOrder(3)]
    public DateTime PostingDate
    {
        get => DateTime.SpecifyKind(field, DateTimeKind.Utc);
        set => field = DateTime.SpecifyKind(value.Date, DateTimeKind.Utc);
    }

    private decimal _value;

    // Dormant running units, dropped in #1144.
    [JsonIgnore]
    public decimal Value
    {
        get => _value;
        set => _value = value;
    }

    /// <summary>Optional broker fee. Retained because fees affect the real cost basis.</summary>
    public decimal? Fee { get; set; }

    /// <summary>Timestamp when the record was created in this system.</summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>Timestamp when the record was last updated in this system.</summary>
    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>Signed change in investment units, derived from the trade.</summary>
    [JsonIgnore]
    public decimal ValueChange
    {
        get => SignedQuantity;
        set
        {
            Quantity = Math.Abs(value);
            if (value > 0) Type = InvestmentTransactionType.Buy;
            else if (value < 0) Type = InvestmentTransactionType.Sell;
        }
    }

    /// <summary>Signed quantity delta this transaction contributes to a holding (+ for Buy, - for Sell).</summary>
    public decimal SignedQuantity => Type switch
    {
        InvestmentTransactionType.Buy => Quantity,
        InvestmentTransactionType.Sell => -Quantity,
        _ => 0m
    };

    [JsonInclude]
    [JsonPropertyOrder(1)]
    public long EntryId { get; internal set; }

    [JsonPropertyOrder(-1)]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyOrder(6)]
    public ICollection<FinancialLabel> Labels { get; set; } = [];

    // Dormant account link, dropped in #1144.
    [JsonIgnore]
    public int? AccountId { get; set; }
}