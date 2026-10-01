using FinanceManager.Components.Shared.Models;
using FinanceManager.Domain.MoneyFlow.Entities;

namespace FinanceManager.Components.Features.Dashboard.Models;

public sealed class AssetsDistributionSnapshot : SnapshotBase
{
    public int UserId { get; set; }
    public int CurrencyId { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime AsOfDate { get; set; }
    public List<NameValueResult> TypeData { get; set; } = [];
    public List<NameValueResult> AccountData { get; set; } = [];
}