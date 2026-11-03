using FinanceManager.Application.Alerts.Models;

namespace FinanceManager.Application.Alerts.Services.Conditions;

public sealed record AlertConditionResult(
    bool ConditionMet,
    decimal ObservedValue,
    string Fingerprint,
    string Message,
    IReadOnlyDictionary<string, string> Context,
    IReadOnlyList<AlertTransactionReference>? MatchingTransactions = null,
    int MatchingTransactionCount = 0,
    int? OccurrenceCount = null);