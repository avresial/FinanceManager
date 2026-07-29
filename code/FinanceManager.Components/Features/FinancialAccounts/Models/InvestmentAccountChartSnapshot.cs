using FinanceManager.Components.Shared.Models;

namespace FinanceManager.Components.Features.FinancialAccounts.Models;

public sealed class InvestmentAccountChartSnapshot : SnapshotBase
{
    public int UserId { get; set; }
    public int AccountId { get; set; }
    public int CurrencyId { get; set; }
    public long? BenchmarkListingId { get; set; }
    public required string RangeKey { get; set; }
    public required InvestmentAccountChartModel Model { get; set; }
}