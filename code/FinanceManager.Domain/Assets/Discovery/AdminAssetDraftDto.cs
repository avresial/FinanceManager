using FinanceManager.Domain.Assets.Dtos;

namespace FinanceManager.Domain.Assets.Discovery;

public sealed class AdminAssetDraftDto
{
    public required AssetDto Asset { get; init; }
    public IReadOnlyList<InstrumentDiscoveryResultDto> Listings { get; init; } = [];
}