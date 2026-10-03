using FinanceManager.Components.Shared.Models;
using FinanceManager.Domain.Shared.Charting;

namespace FinanceManager.Components.Features.Administration.Components;

public class DailyActiveUsersSnapshot : SnapshotBase
{
    public List<ChartEntryModel> Entries { get; set; } = [];
}