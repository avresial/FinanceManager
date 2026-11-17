using FinanceManager.Contracts.Assets;
using FinanceManager.Contracts.FinancialAccounts.Shared;

namespace FinanceManager.Contracts.FinancialAccounts.Investments;

/// <summary>Request to add a new investment transaction. The owning user is derived from the account, not this payload.</summary>
public record AddInvestmentTransactionRequest(
    int AccountId,
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