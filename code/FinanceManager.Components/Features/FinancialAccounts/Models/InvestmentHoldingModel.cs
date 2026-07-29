namespace FinanceManager.Components.Features.FinancialAccounts.Models;

public sealed record InvestmentHoldingModel(
    long ListingId,
    string Ticker,
    string ExchangeName,
    string Currency,
    decimal Quantity,
    decimal LatestPrice,
    decimal Value);