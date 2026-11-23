using FinanceManager.Domain.Transactions.Cash.Dtos;

namespace FinanceManager.Domain.Dashboard.Dtos;

public record CurrencyTransactionLogItem(CashLogEntry Entry) : TransactionLogItem(Entry.LegacyAccountId);