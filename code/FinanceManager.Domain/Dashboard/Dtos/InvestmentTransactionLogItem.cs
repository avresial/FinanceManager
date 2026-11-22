using FinanceManager.Domain.FinancialAccounts.Investments.Entities;

namespace FinanceManager.Domain.Dashboard.Dtos;

public record InvestmentTransactionLogItem(int AccountId, string AccountName, InvestmentTransaction Transaction)
    : TransactionLogItem(AccountId, AccountName);