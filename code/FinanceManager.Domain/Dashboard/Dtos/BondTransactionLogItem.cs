using FinanceManager.Domain.FinancialAccounts.Bond.Entities;

namespace FinanceManager.Domain.Dashboard.Dtos;

public record BondTransactionLogItem(int AccountId, string AccountName, BondAccountEntry Entry)
    : TransactionLogItem(AccountId, AccountName);