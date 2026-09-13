using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;

namespace FinanceManager.Domain.Alerts.Models;

public sealed record FinancialAlertEvaluationData(
    decimal TotalSpend,
    int TransactionCount,
    CurrencyAccountEntry? LargestTransaction);