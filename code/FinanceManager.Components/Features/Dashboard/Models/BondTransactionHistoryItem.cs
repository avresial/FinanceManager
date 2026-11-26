using FinanceManager.Domain.Dashboard.Dtos;
using FinanceManager.Domain.Transactions.Bonds.Entities;

namespace FinanceManager.Components.Features.Dashboard.Models;

public record BondTransactionHistoryItem(BondEntry Entry, string? BondName = null)
    : TransactionHistoryItem
{
    public override TransactionKind Kind => TransactionKind.Bond;
    public override long EntryId => Entry.EntryId;
    public override DateTime Date => Entry.PostingDate;
}