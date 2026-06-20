namespace FinanceManager.Domain.Assets.Entities;

/// <summary>
/// Represents the financial instrument itself (a share class or company), independent of any
/// specific exchange listing. Examples: "Apple Inc.", "iShares Core S&amp;P 500 UCITS ETF USD (Acc)".
/// Tickers are not globally unique, so the exact tradeable venue lives on <see cref="AssetListing"/>.
/// </summary>
public class Asset
{
    /// <summary>Internal database identifier.</summary>
    public long Id { get; set; }

    /// <summary>Human-readable name of the asset, e.g. "iShares Core S&amp;P 500 UCITS ETF USD (Acc)".</summary>
    public string Name { get; set; } = null!;

    /// <summary>Type of asset (Stock, ETF, Bond, Crypto, ...).</summary>
    public AssetType Type { get; set; }

    /// <summary>Main ISIN of the asset/share class, e.g. "IE00B5BMR087". Identifies the security but not the exact listing.</summary>
    public string? Isin { get; set; }

    /// <summary>FIGI identifying the share class. Usually stable across listings of the same share class.</summary>
    public string? ShareClassFigi { get; set; }

    /// <summary>FIGI identifying the composite/security across venues. Useful for grouping listings of the same security.</summary>
    public string? CompositeFigi { get; set; }

    /// <summary>Company, fund provider, or issuer, e.g. "iShares", "Vanguard", "Apple Inc.".</summary>
    public string? Issuer { get; set; }

    /// <summary>Country where the asset/fund is domiciled, e.g. "Ireland", "United States".</summary>
    public string? Domicile { get; set; }

    /// <summary>Base/reporting currency of the fund or company, e.g. "USD". Not necessarily the trading currency.</summary>
    public string? BaseCurrency { get; set; }

    /// <summary>ETF-specific: whether the fund accumulates or distributes dividends.</summary>
    public DistributionPolicy? DistributionPolicy { get; set; }

    /// <summary>ETF-specific: tracked benchmark, e.g. "S&amp;P 500".</summary>
    public string? BenchmarkIndex { get; set; }

    /// <summary>ETF-specific: replication method (Physical, Synthetic, Sampling).</summary>
    public ReplicationMethod? ReplicationMethod { get; set; }

    /// <summary>ETF-specific: total expense ratio, e.g. 0.0007 for 0.07%.</summary>
    public decimal? TotalExpenseRatio { get; set; }

    /// <summary>Whether this is UCITS-compliant (mostly relevant for European ETFs).</summary>
    public bool? IsUcits { get; set; }

    /// <summary>Date when the asset/fund/share class started trading.</summary>
    public DateOnly? InceptionDate { get; set; }

    /// <summary>Timestamp when the record was created in this system.</summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>Timestamp when the record was last updated in this system.</summary>
    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>Exchange listings for this asset.</summary>
    public ICollection<AssetListing> Listings { get; set; } = [];

    /// <summary>Additional identifiers such as ISIN, FIGI, CUSIP, SEDOL, WKN.</summary>
    public ICollection<AssetIdentifier> Identifiers { get; set; } = [];
}