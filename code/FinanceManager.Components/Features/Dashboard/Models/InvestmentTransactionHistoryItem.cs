using FinanceManager.Contracts.FinancialAccounts.Investments;
using FinanceManager.Domain.Dashboard.Dtos;

namespace FinanceManager.Components.Features.Dashboard.Models;

public record InvestmentTransactionHistoryItem(InvestmentTransactionDto Transaction)
    : TransactionHistoryItem(Transaction.AccountId)
{
    public override TransactionKind Kind => TransactionKind.Investment;
    public override long EntryId => Transaction.Id;

    // Trades are date-only and are stored at UTC midnight.
    public override DateTime Date => Transaction.TradeDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
}