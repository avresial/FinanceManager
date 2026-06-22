using FinanceManager.Domain.FinancialAccounts.Investments.Entities;

namespace FinanceManager.Domain.FinancialAccounts.Investments.Dtos;

/// <summary>Read model for an <see cref="InvestmentTransaction"/> returned over the API.</summary>
public record InvestmentTransactionDto(
    long Id,
    long UserId,
    int AccountId,
    long AssetListingId,
    InvestmentTransactionType Type,
    decimal Quantity,
    decimal UnitPrice,
    string Currency,
    DateOnly TradeDate,
    decimal? Fee,
    string? Notes,
    string Ticker = "",
    string ExchangeName = "");

/// <summary>Request to add a new investment transaction. The owning user is derived from the account, not this payload.</summary>
public record AddInvestmentTransactionRequest(
    int AccountId,
    long AssetListingId,
    InvestmentTransactionType Type,
    decimal Quantity,
    decimal UnitPrice,
    string Currency,
    DateOnly TradeDate,
    decimal? Fee = null,
    string? Notes = null);

/// <summary>Request to update an existing investment transaction. Account and owner are not reassigned.</summary>
public record UpdateInvestmentTransactionRequest(
    long Id,
    int AccountId,
    long AssetListingId,
    InvestmentTransactionType Type,
    decimal Quantity,
    decimal UnitPrice,
    string Currency,
    DateOnly TradeDate,
    decimal? Fee = null,
    string? Notes = null);