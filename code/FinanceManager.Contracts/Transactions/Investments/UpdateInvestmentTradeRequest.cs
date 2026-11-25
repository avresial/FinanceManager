
namespace FinanceManager.Contracts.Transactions.Investments;

/// <summary>Request to change a trade of the caller's portfolio. The trade id comes from the route; the owner is never reassigned. Omitted <see cref="LabelIds"/> keeps the labels, an empty list clears them.</summary>
public record UpdateInvestmentTradeRequest(
    long AssetListingId,
    InvestmentTransactionTypeDto Type,
    decimal Quantity,
    decimal UnitPrice,
    string Currency,
    DateOnly TradeDate,
    decimal? Fee = null,
    string? Notes = null,
    IReadOnlyCollection<int>? LabelIds = null);