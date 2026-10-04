using FinanceManager.Components.Shared.Models;

namespace FinanceManager.Components.Features.Administration.Models;

public sealed class AdminTotalTrackedMoneySnapshot : SnapshotBase
{
    public int UserId { get; set; }
    public decimal Amount { get; set; }
}