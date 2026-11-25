namespace FinanceManager.Domain.FinancialAccounts.Shared.Entities;

/// <summary>An owner's dated change in cash or bond units.</summary>
public interface ILedgerEntry
{
    long EntryId { get; }

    int UserId { get; }
    DateTime PostingDate { get; }

    decimal ValueChange { get; }
}