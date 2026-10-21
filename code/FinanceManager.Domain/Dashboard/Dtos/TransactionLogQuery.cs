using FinanceManager.Domain.FinancialAccounts.Shared.Entities;

namespace FinanceManager.Domain.Dashboard.Dtos;

/// <summary>
/// Inclusive timestamp range; timestamps without a time zone are interpreted as UTC.
/// <paramref name="AccountTypes"/> and <paramref name="AccountIds"/> narrow the log to those account types and
/// accounts (AND-combined); null or empty means no filter. IDs that do not belong to the user are ignored.
/// </summary>
public record TransactionLogQuery(
    DateTime Start,
    DateTime End,
    int PageSize = 25,
    TransactionLogCursor? Cursor = null,
    IReadOnlyList<AccountType>? AccountTypes = null,
    IReadOnlyList<int>? AccountIds = null);