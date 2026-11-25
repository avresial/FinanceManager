using FinanceManager.Domain.Dashboard.Dtos;
using FinanceManager.Domain.Transactions.Cash.Entities;

namespace FinanceManager.Components.Features.Dashboard.Models;

public record CurrencyTransactionHistoryItem(string Currency, CashEntry Entry)
    : TransactionHistoryItem
{
    public override TransactionKind Kind => TransactionKind.Cash;
    public override long EntryId => Entry.EntryId;
    public override DateTime Date => Entry.PostingDate;
}