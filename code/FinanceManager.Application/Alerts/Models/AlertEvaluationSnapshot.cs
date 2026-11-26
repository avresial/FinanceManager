using FinanceManager.Domain.Alerts.Models;
using FinanceManager.Domain.Labels.Entities;
using FinanceManager.Domain.Transactions.Cash.Dtos;
using FinanceManager.Domain.Transactions.Shared.Dtos;

namespace FinanceManager.Application.Alerts.Models;

public class AlertEvaluationSnapshot
{
    /// <summary>Pooled cash balance per currency (partition id = currency id).</summary>
    public IReadOnlyList<OwnerLedgerPosition> CashPositions { get; init; } = [];
    public IReadOnlyDictionary<int, string> CurrencyCodes { get; init; } = new Dictionary<int, string>();
    public IReadOnlyList<CashLogEntry> RecentEntries { get; init; } = [];
    public IReadOnlyList<RecurringTransactionResult> Subscriptions { get; init; } = [];
    public IReadOnlyDictionary<Guid, FinancialAlertEvaluationData> AllTimeEvaluationData { get; init; } = new Dictionary<Guid, FinancialAlertEvaluationData>();
    public DateTime EvaluationDate { get; init; } = DateTime.UtcNow;
    public bool IncludeAllMatchingTransactions { get; init; }
}