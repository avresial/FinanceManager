using FinanceManager.Domain.Dashboard.Dtos;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;

namespace FinanceManager.Components.Features.Dashboard.Models;

public record CurrencyTransactionHistoryItem(string Currency, CurrencyAccountEntry Entry)
    : TransactionHistoryItem
{
    public override TransactionKind Kind => TransactionKind.Cash;
    public override long EntryId => Entry.EntryId;
    public override DateTime Date => Entry.PostingDate;
}