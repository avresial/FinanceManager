using FinanceManager.Domain.Transactions.Bonds.Dtos;

namespace FinanceManager.Domain.Dashboard.Dtos;

public record BondTransactionLogItem(BondLogEntry Entry) : TransactionLogItem(Entry.LegacyAccountId);