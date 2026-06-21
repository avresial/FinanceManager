using FinanceManager.Domain.Assets.Entities;

namespace FinanceManager.Domain.Assets.Dtos;

/// <summary>
/// Mapping helpers between the asset entities and their flat DTOs. Entity-to-DTO drops navigation
/// back-references so the result serialises without cycles; DTO-to-entity ignores audit timestamps
/// (those are stamped by the repositories).
/// </summary>
public static class AssetMappingExtensions
{
    public static AssetDto ToDto(this Asset asset) => new()
    {
        Id = asset.Id,
        Name = asset.Name,
        Type = asset.Type,
        Isin = asset.Isin,
        ShareClassFigi = asset.ShareClassFigi,
        CompositeFigi = asset.CompositeFigi,
        Issuer = asset.Issuer,
        Domicile = asset.Domicile,
        BaseCurrency = asset.BaseCurrency,
        DistributionPolicy = asset.DistributionPolicy,
        BenchmarkIndex = asset.BenchmarkIndex,
        ReplicationMethod = asset.ReplicationMethod,
        TotalExpenseRatio = asset.TotalExpenseRatio,
        IsUcits = asset.IsUcits,
        InceptionDate = asset.InceptionDate,
        CreatedAt = asset.CreatedAt,
        UpdatedAt = asset.UpdatedAt,
        Listings = [.. asset.Listings.Select(l => l.ToDto())]
    };

    public static AssetListingDto ToDto(this AssetListing listing) => new()
    {
        Id = listing.Id,
        AssetId = listing.AssetId,
        Ticker = listing.Ticker,
        ExchangeMic = listing.ExchangeMic,
        ExchangeName = listing.ExchangeName,
        TradingCurrency = listing.TradingCurrency,
        ListingFigi = listing.ListingFigi,
        ExchangeInstrumentId = listing.ExchangeInstrumentId,
        IsPrimaryListing = listing.IsPrimaryListing,
        PriceMultiplier = listing.PriceMultiplier,
        IsActive = listing.IsActive,
        MarketDataSymbols = [.. listing.MarketDataSymbols.Select(s => s.ToDto())]
    };

    public static MarketDataSymbolDto ToDto(this MarketDataSymbol symbol) => new()
    {
        Id = symbol.Id,
        AssetListingId = symbol.AssetListingId,
        Provider = symbol.Provider,
        Symbol = symbol.Symbol,
        ProviderExchangeCode = symbol.ProviderExchangeCode,
        ProviderInstrumentId = symbol.ProviderInstrumentId,
        Currency = symbol.Currency,
        IsPrimary = symbol.IsPrimary,
        IsEnabled = symbol.IsEnabled
    };

    /// <summary>Trim a value, collapsing null/whitespace to null so blank inputs aren't stored as empty strings.</summary>
    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    /// <summary>Copy the editable fields of <paramref name="dto"/> onto <paramref name="asset"/> (timestamps excluded).</summary>
    public static void ApplyTo(this AssetDto dto, Asset asset)
    {
        asset.Name = dto.Name.Trim();
        asset.Type = dto.Type;
        asset.Isin = Normalize(dto.Isin);
        asset.ShareClassFigi = Normalize(dto.ShareClassFigi);
        asset.CompositeFigi = Normalize(dto.CompositeFigi);
        asset.Issuer = Normalize(dto.Issuer);
        asset.Domicile = Normalize(dto.Domicile);
        asset.BaseCurrency = Normalize(dto.BaseCurrency);
        asset.DistributionPolicy = dto.DistributionPolicy;
        asset.BenchmarkIndex = Normalize(dto.BenchmarkIndex);
        asset.ReplicationMethod = dto.ReplicationMethod;
        asset.TotalExpenseRatio = dto.TotalExpenseRatio;
        asset.IsUcits = dto.IsUcits;
        asset.InceptionDate = dto.InceptionDate;
    }

    public static Asset ToNewEntity(this AssetDto dto)
    {
        var asset = new Asset();
        dto.ApplyTo(asset);
        return asset;
    }

    /// <summary>Copy the editable fields of <paramref name="dto"/> onto <paramref name="listing"/> (Id/AssetId/timestamps excluded).</summary>
    public static void ApplyTo(this AssetListingDto dto, AssetListing listing)
    {
        listing.Ticker = dto.Ticker.Trim();
        listing.ExchangeMic = dto.ExchangeMic.Trim();
        listing.ExchangeName = dto.ExchangeName.Trim();
        listing.TradingCurrency = dto.TradingCurrency.Trim();
        listing.ListingFigi = Normalize(dto.ListingFigi);
        listing.ExchangeInstrumentId = Normalize(dto.ExchangeInstrumentId);
        listing.IsPrimaryListing = dto.IsPrimaryListing;
        listing.PriceMultiplier = dto.PriceMultiplier;
        listing.IsActive = dto.IsActive;
    }

    public static AssetListing ToNewEntity(this AssetListingDto dto, long assetId)
    {
        var listing = new AssetListing { AssetId = assetId };
        dto.ApplyTo(listing);
        return listing;
    }

    /// <summary>Copy the editable fields of <paramref name="dto"/> onto <paramref name="symbol"/> (Id/AssetListingId/timestamps excluded).</summary>
    public static void ApplyTo(this MarketDataSymbolDto dto, MarketDataSymbol symbol)
    {
        symbol.Provider = dto.Provider;
        symbol.Symbol = dto.Symbol.Trim();
        symbol.ProviderExchangeCode = Normalize(dto.ProviderExchangeCode);
        symbol.ProviderInstrumentId = Normalize(dto.ProviderInstrumentId);
        symbol.Currency = Normalize(dto.Currency);
        symbol.IsPrimary = dto.IsPrimary;
        symbol.IsEnabled = dto.IsEnabled;
    }

    public static MarketDataSymbol ToNewEntity(this MarketDataSymbolDto dto, long assetListingId)
    {
        var symbol = new MarketDataSymbol { AssetListingId = assetListingId };
        dto.ApplyTo(symbol);
        return symbol;
    }
}