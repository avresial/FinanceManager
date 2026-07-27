using FinanceManager.Domain.FinancialAccounts.Shared.Entities;

namespace FinanceManager.Components.Features.FinancialAccounts.Models;

/// <summary>
/// The account-details transaction list as rendered: everything the page needs to rebuild the
/// account, and nothing else. Content equality between a stored snapshot and a fresh API response
/// is evaluated on this model, so snapshot metadata can never look like a change.
/// </summary>
/// <typeparam name="TEntry">The concrete entry type (e.g. CurrencyAccountEntry).</typeparam>
public sealed record AccountDetailsModel<TEntry>(
    int UserId,
    int AccountId,
    string Name,
    AccountLabel AccountType,
    List<TEntry> Entries);