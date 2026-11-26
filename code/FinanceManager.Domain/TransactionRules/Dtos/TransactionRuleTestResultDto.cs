using FinanceManager.Domain.TransactionRules.Models;

namespace FinanceManager.Domain.TransactionRules.Dtos;

public sealed record TransactionRuleTestResultDto(
    long TransactionId,
    DateTime Date,
    decimal Amount,
    string? CurrencyCode,
    TransactionFacts Before,
    TransactionFacts After,
    bool HasChanges);