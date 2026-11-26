using FinanceManager.Contracts.Assets;

namespace FinanceManager.Contracts.Transactions.Investments;

/// <summary>Signed units of one listing held across all of the caller's trades. Oversold positions are negative.</summary>
public record InvestmentHoldingDto(
    long AssetListingId,
    string Ticker,
    string ExchangeName,
    AssetTypeDto AssetType,
    decimal Units);