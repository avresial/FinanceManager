namespace FinanceManager.Domain.FinancialAccounts.Investments.Dtos;

public sealed record DividendPaymentDto(long Id, int AccountId, long AssetListingId,
    string HoldingName, string Ticker, DateOnly PaymentDate, decimal Amount, string Currency, string? Notes);