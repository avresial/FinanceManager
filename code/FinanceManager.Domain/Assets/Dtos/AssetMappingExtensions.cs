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

    /// <summary>Copy the editable fields of <paramref name="dto"/> onto <paramref name="asset"/> (timestamps excluded).</summary>
    public static void ApplyTo(this AssetDto dto, Asset asset)
    {
        asset.Name = dto.Name;
        asset.Type = dto.Type;
        asset.Isin = string.IsNullOrWhiteSpace(dto.Isin) ? null : dto.Isin.Trim();
        asset.ShareClassFigi = dto.ShareClassFigi;
        asset.CompositeFigi = dto.CompositeFigi;
        asset.Issuer = dto.Issuer;
        asset.Domicile = dto.Domicile;
        asset.BaseCurrency = dto.BaseCurrency;
        asset.DistributionPolicy = dto.DistributionPolicy;
        asset.BenchmarkIndex = dto.BenchmarkIndex;
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
        listing.ExchangeName = dto.ExchangeName;
        listing.TradingCurrency = dto.TradingCurrency.Trim();
        listing.ListingFigi = dto.ListingFigi;
        listing.ExchangeInstrumentId = dto.ExchangeInstrumentId;
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
        symbol.ProviderExchangeCode = dto.ProviderExchangeCode;
        symbol.ProviderInstrumentId = dto.ProviderInstrumentId;
        symbol.Currency = dto.Currency;
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