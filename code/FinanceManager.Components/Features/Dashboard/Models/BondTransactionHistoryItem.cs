using FinanceManager.Domain.Dashboard.Dtos;
using FinanceManager.Domain.FinancialAccounts.Bond.Entities;

namespace FinanceManager.Components.Features.Dashboard.Models;

public record BondTransactionHistoryItem(BondAccountEntry Entry, string? BondName = null)
    : TransactionHistoryItem(Entry.AccountId)
{
    public override TransactionKind Kind => TransactionKind.Bond;
    public override long EntryId => Entry.EntryId;
    public override DateTime Date => Entry.PostingDate;
}