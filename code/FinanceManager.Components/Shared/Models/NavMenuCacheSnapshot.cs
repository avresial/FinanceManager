namespace FinanceManager.Components.Shared.Models;

public sealed class NavMenuCacheSnapshot
{
    public const int CurrentSchemaVersion = 3;

    public int SchemaVersion { get; set; } = CurrentSchemaVersion;
    public int UserId { get; set; }
    public DateTime FetchedAtUtc { get; set; }
    public bool HasTransactions { get; set; }
    public bool DisplayAssetsLink { get; set; }
    public bool DisplayLiabilitiesLink { get; set; }
}