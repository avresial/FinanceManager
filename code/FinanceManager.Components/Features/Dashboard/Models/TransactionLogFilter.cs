using FinanceManager.Domain.Dashboard.Dtos;

namespace FinanceManager.Components.Features.Dashboard.Models;

/// <summary>Kind and currency filters of the Transactions page; an empty list means "no restriction" for that dimension.</summary>
public sealed record TransactionLogFilter(IReadOnlyList<TransactionKind> Kinds, IReadOnlyList<string> Currencies)
{
    public static readonly TransactionLogFilter None = new([], []);

    public bool IsActive => Kinds.Count != 0 || Currencies.Count != 0;
}