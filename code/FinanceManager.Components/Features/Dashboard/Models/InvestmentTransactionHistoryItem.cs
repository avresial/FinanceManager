using FinanceManager.Contracts.FinancialAccounts.Investments;
using FinanceManager.Domain.FinancialAccounts.Shared.Entities;

namespace FinanceManager.Components.Features.Dashboard.Models;

public record InvestmentTransactionHistoryItem(int AccountId, string AccountName, InvestmentTransactionDto Transaction)
    : TransactionHistoryItem(AccountId, AccountName)
{
    public override AccountType AccountType => AccountType.Stock;
    public override long EntryId => Transaction.Id;

    // Trades are date-only and are stored at UTC midnight.
    public override DateTime Date => Transaction.TradeDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
}