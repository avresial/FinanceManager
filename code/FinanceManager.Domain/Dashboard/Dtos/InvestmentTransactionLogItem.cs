using FinanceManager.Domain.FinancialAccounts.Investments.Entities;

namespace FinanceManager.Domain.Dashboard.Dtos;

public record InvestmentTransactionLogItem(InvestmentTransaction Transaction) : TransactionLogItem;