
namespace FinanceManager.Contracts.Transactions.Investments;

/// <summary>Request to add a trade to the caller's pooled portfolio. The owner comes from the authenticated principal; there is no account.</summary>
public record CreateInvestmentTradeRequest(
    long? AssetListingId,
    InvestmentTransactionTypeDto Type,
    decimal Quantity,
    decimal UnitPrice,
    string Currency,
    DateOnly TradeDate,
    decimal? Fee = null,
    string? Notes = null,
    string? ExternalInstrumentResultId = null,
    IReadOnlyCollection<int>? LabelIds = null);