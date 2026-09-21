using FinanceManager.Domain.TransactionRules.Models;

namespace FinanceManager.Domain.TransactionRules.Dtos;

public sealed record TransactionRuleTestResultDto(
    int AccountId,
    string AccountName,
    int TransactionId,
    DateTime Date,
    decimal Amount,
    TransactionFacts Before,
    TransactionFacts After,
    bool HasChanges);