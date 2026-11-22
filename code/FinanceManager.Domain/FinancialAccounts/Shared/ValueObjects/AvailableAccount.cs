using FinanceManager.Domain.FinancialAccounts.Shared.Entities;
using System.Text.Json.Serialization;

namespace FinanceManager.Domain.FinancialAccounts.Shared.ValueObjects;

/// <summary>
/// <paramref name="CurrencyId"/> is server-side only (currency accounts) and is kept off the wire so the
/// account-list payloads stay unchanged.
/// </summary>
public record AvailableAccount(int AccountId, string AccountName, AccountLabel? AccountLabel = null, string? Currency = null,
    [property: JsonIgnore] int? CurrencyId = null);