namespace FinanceManager.Domain.FinancialAccounts.Investments.Dtos;

/// <summary>
/// Valuation and performance figures for a single <see cref="Entities.InvestmentTransaction"/> Buy,
/// used by the transaction detail view. All monetary figures are expressed in <see cref="Currency"/>:
/// the user's default currency when <see cref="IsConverted"/> is <c>true</c>, otherwise the
/// instrument/transaction currency (values are never mixed across currencies).
/// </summary>
public record InvestmentTransactionValuationDto(
    long TransactionId,
    decimal PurchaseValue,
    decimal CurrentPrice,
    decimal CurrentValuation,
    decimal GainLoss,
    decimal GainLossPercent,
    string Currency,
    bool IsConverted,
    bool HasCurrentPrice);