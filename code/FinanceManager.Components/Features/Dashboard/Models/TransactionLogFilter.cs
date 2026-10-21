using FinanceManager.Domain.FinancialAccounts.Shared.Entities;

namespace FinanceManager.Components.Features.Dashboard.Models;

/// <summary>Account filters of the Transactions page; an empty list means "no restriction" for that dimension.</summary>
public sealed record TransactionLogFilter(IReadOnlyList<AccountType> AccountTypes, IReadOnlyList<int> AccountIds)
{
    public static readonly TransactionLogFilter None = new([], []);

    public bool IsActive => AccountTypes.Count != 0 || AccountIds.Count != 0;
}