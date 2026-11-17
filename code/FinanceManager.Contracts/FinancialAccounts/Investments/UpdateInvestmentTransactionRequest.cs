using FinanceManager.Contracts.Assets;
using FinanceManager.Contracts.FinancialAccounts.Shared;

namespace FinanceManager.Contracts.FinancialAccounts.Investments;

/// <summary>Request to update an existing investment transaction. Account and owner are not reassigned.</summary>
public record UpdateInvestmentTransactionRequest(
    long Id,
    int AccountId,
    long AssetListingId,
    InvestmentTransactionTypeDto Type,
    decimal Quantity,
    decimal UnitPrice,
    string Currency,
    DateOnly TradeDate,
    decimal? Fee = null,
    string? Notes = null,
    IReadOnlyCollection<int>? LabelIds = null);