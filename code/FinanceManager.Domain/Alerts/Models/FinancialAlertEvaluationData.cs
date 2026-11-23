using FinanceManager.Domain.Transactions.Cash.Dtos;

namespace FinanceManager.Domain.Alerts.Models;

public sealed record FinancialAlertEvaluationData(
    decimal TotalSpend,
    int TransactionCount,
    CashLogEntry? LargestTransaction,
    IReadOnlyList<CashLogEntry>? MatchingTransactions = null);