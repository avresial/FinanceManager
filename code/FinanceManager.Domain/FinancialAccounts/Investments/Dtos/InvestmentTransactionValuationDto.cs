namespace FinanceManager.Domain.FinancialAccounts.Investments.Dtos;

/// <summary>
/// Valuation and performance figures for a single <see cref="Entities.InvestmentTransaction"/> Buy,
/// used by the transaction detail view. All monetary figures are expressed in <see cref="Currency"/>:
/// the user's default currency when <see cref="IsConverted"/> is <c>true</c>, otherwise the
/// instrument/transaction currency (values are never mixed across currencies).
/// <see cref="PurchaseUnitPrice"/> and <see cref="CurrentPrice"/> are per-unit prices (then vs now);
/// <see cref="PurchaseValue"/> and <see cref="CurrentValuation"/> are the whole position's value (then vs now).
/// </summary>
public record InvestmentTransactionValuationDto(
    long TransactionId,
    decimal PurchaseUnitPrice,
    decimal PurchaseValue,
    decimal CurrentPrice,
    decimal CurrentValuation,
    decimal GainLoss,
    decimal GainLossPercent,
    string Currency,
    bool IsConverted,
    bool HasCurrentPrice);