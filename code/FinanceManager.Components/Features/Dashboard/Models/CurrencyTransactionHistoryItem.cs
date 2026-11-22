using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.FinancialAccounts.Shared.Entities;

namespace FinanceManager.Components.Features.Dashboard.Models;

public record CurrencyTransactionHistoryItem(CurrencyAccount Account, string Currency, CurrencyAccountEntry Entry)
    : TransactionHistoryItem(Account.AccountId, Account.Name)
{
    public override AccountType AccountType => AccountType.Currency;
    public override long EntryId => Entry.EntryId;
    public override DateTime Date => Entry.PostingDate;
}