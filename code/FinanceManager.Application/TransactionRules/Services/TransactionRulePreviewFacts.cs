using FinanceManager.Domain.TransactionRules.Models;

namespace FinanceManager.Application.TransactionRules.Services;

/// <summary>Preview facts are kept separate from command records so the endpoint has a stable JSON shape.</summary>
public sealed record TransactionRulePreviewFacts(
    string Contractor,
    string Description,
    decimal Amount,
    TransactionDirection Direction,
    IReadOnlyList<string> Labels);