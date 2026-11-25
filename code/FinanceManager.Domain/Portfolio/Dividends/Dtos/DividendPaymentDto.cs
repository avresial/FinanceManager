namespace FinanceManager.Domain.Portfolio.Dividends.Dtos;

public sealed record DividendPaymentDto(long Id, long AssetListingId,
    string HoldingName, string Ticker, DateOnly PaymentDate, decimal Amount, string Currency, string? Notes);