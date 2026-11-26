using FinanceManager.Contracts.Assets;
using FinanceManager.Contracts.FinancialAccounts.Shared;

namespace FinanceManager.Contracts.FinancialAccounts.Investments;

/// <summary>Investment transaction returned over the API.</summary>
public record InvestmentTransactionDto(
    long Id,
    long UserId,
    long AssetListingId,
    InvestmentTransactionTypeDto Type,
    decimal Quantity,
    decimal UnitPrice,
    string Currency,
    DateOnly TradeDate,
    decimal? Fee,
    string? Notes,
    string Ticker = "",
    string ExchangeName = "",
    AssetTypeDto AssetType = AssetTypeDto.Other,
    bool AutomaticPricesAvailable = true,
    IReadOnlyList<FinancialLabelDto>? Labels = null);