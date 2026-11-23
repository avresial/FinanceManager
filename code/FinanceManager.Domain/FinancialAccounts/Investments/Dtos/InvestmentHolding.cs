using FinanceManager.Domain.Assets.Entities;

namespace FinanceManager.Domain.FinancialAccounts.Investments.Dtos;

/// <summary>Signed units of one listing held by an owner as of a date, with the listing metadata needed to display it.</summary>
public sealed record InvestmentHolding(long AssetListingId, string Ticker, string ExchangeName, AssetType AssetType, decimal Units);