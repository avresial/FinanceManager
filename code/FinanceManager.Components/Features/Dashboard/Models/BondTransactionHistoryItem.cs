using FinanceManager.Domain.FinancialAccounts.Bond.Entities;
using FinanceManager.Domain.FinancialAccounts.Shared.Entities;

namespace FinanceManager.Components.Features.Dashboard.Models;

public record BondTransactionHistoryItem(BondAccount Account, BondAccountEntry Entry)
    : TransactionHistoryItem(Account.AccountId, Account.Name)
{
    public override AccountType AccountType => AccountType.Bond;
    public override long EntryId => Entry.EntryId;
    public override DateTime Date => Entry.PostingDate;
}