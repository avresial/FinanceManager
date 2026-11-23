using System.Text.Json.Serialization;

namespace FinanceManager.Domain.TransactionRules.Commands;

/// <summary>
/// Unmapped members are rejected so a legacy account-scoped request (<c>accountId</c>) can never be read as "apply to everything".
/// </summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ApplyTransactionRules(
    bool Confirmed,
    long? EntryId = null);