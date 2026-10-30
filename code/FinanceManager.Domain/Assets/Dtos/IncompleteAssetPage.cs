namespace FinanceManager.Domain.Assets.Dtos;

public sealed record IncompleteAssetPage(IReadOnlyList<IncompleteAssetItem> Items, int TotalCount, int Page, int PageSize);