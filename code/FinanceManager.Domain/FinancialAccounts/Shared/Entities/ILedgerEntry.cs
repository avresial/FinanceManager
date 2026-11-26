namespace FinanceManager.Domain.FinancialAccounts.Shared.Entities;

/// <summary>A dated change and running balance within an account's chosen unit and instrument scope.</summary>
public interface ILedgerEntry
{
    long EntryId { get; }
    int? AccountId { get; }
    int UserId { get; }
    DateTime PostingDate { get; }
    decimal Value { get; set; }
    decimal ValueChange { get; }
}