namespace FinanceManager.Domain.Assets.Entities;

/// <summary>
/// Represents an additional identifier assigned to an <see cref="Asset"/>. Not every instrument
/// has every identifier, and some identifiers apply at different scopes (asset, share class, listing).
/// </summary>
public class AssetIdentifier
{
    /// <summary>Internal database identifier.</summary>
    public long Id { get; set; }

    /// <summary>Asset this identifier belongs to.</summary>
    public long AssetId { get; set; }

    /// <summary>Asset this identifier belongs to.</summary>
    public Asset Asset { get; set; } = null!;

    /// <summary>Identifier type (ISIN, FIGI, CompositeFIGI, ShareClassFIGI, CUSIP, SEDOL, WKN, ...).</summary>
    public AssetIdentifierType Type { get; set; }

    /// <summary>Identifier value, e.g. "IE00B5BMR087".</summary>
    public string Value { get; set; } = null!;

    /// <summary>Scope of the identifier (Asset, ShareClass, Listing, Exchange, Provider).</summary>
    public IdentifierScope Scope { get; set; }

    /// <summary>Source from which this identifier was obtained, e.g. "OpenFIGI", "Manual", "BrokerImport".</summary>
    public string? Source { get; set; }

    /// <summary>Whether this is the preferred identifier of this type.</summary>
    public bool IsPrimary { get; set; }

    /// <summary>Timestamp when the record was created in this system.</summary>
    public DateTimeOffset CreatedAt { get; set; }
}