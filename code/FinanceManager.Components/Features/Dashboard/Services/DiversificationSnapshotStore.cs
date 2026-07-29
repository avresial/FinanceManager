using FinanceManager.Components.Features.Dashboard.Models;
using FinanceManager.Components.Shared.Models;
using FinanceManager.Components.Shared.Services;
using FinanceManager.Domain.Insights.Entities;

namespace FinanceManager.Components.Features.Dashboard.Services;

public sealed class DiversificationSnapshotStore(ISnapshotRefreshCoordinator coordinator)
{
    public Task<SnapshotRefreshResult<DiversificationScore>> RefreshAsync(
        int userId,
        DateTime asOfDate,
        RefreshVersionGate gate,
        int? claimedVersion,
        Func<Task<DiversificationScore?>> fetchAsync,
        Func<DiversificationScore, Task>? onSnapshotPainted = null,
        Func<Task>? onSnapshotMissing = null,
        Func<DiversificationScore, Task>? onRefreshed = null) =>
        coordinator.RunAsync(new SnapshotRefreshRequest<DiversificationScoreSnapshot, DiversificationScore>
        {
            Key = $"diversification-score:{userId}:{asOfDate:yyyy-MM-dd}",
            Gate = gate,
            ClaimedVersion = claimedVersion,
            ToModel = snapshot => snapshot.UserId == userId && snapshot.AsOfDate.Date == asOfDate.Date
                ? snapshot.Score
                : null,
            FetchAsync = fetchAsync,
            ToSnapshot = score => new DiversificationScoreSnapshot
            {
                UserId = userId,
                AsOfDate = asOfDate,
                Score = score
            },
            OnSnapshotPainted = onSnapshotPainted,
            OnSnapshotMissing = onSnapshotMissing,
            OnRefreshed = onRefreshed
        });
}