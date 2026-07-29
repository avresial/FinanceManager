using FinanceManager.Components.Shared.Models;

namespace FinanceManager.Components.Features.FinancialAccounts.Models;

public sealed class AccountChartSnapshot : SnapshotBase
{
    public required string Variant { get; set; }
    public int UserId { get; set; }
    public int AccountId { get; set; }
    public int CurrencyId { get; set; }
    public required string RangeKey { get; set; }
    public required AccountChartModel Model { get; set; }
}