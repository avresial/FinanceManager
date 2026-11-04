using FinanceManager.Domain.Labels.Entities;

namespace FinanceManager.Application.Labels.RecurringTransactions;

/// <summary>A detected recurring series with the key and amount used to match it to a stored subscription.</summary>
internal sealed record RecurringPattern(
    string MerchantKey,
    decimal ReferenceAmount,
    RecurringTransactionResult Result);