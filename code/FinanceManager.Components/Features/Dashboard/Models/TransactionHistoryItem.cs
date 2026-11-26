using FinanceManager.Domain.Dashboard.Dtos;

namespace FinanceManager.Components.Features.Dashboard.Models;

/// <summary>
/// One entry of the Transactions page. Each concrete type holds exactly what that account kind's existing
/// history row needs, so the page renders every entry with the same UI as its account page.
/// </summary>
public abstract record TransactionHistoryItem
{
    public abstract TransactionKind Kind { get; }
    public abstract long EntryId { get; }

    /// <summary>UTC timestamp the feed is ordered and grouped by.</summary>
    public abstract DateTime Date { get; }

    /// <summary>Unique across account kinds, whose entry IDs can collide.</summary>
    public string Key => $"{Kind}-{EntryId}";
}