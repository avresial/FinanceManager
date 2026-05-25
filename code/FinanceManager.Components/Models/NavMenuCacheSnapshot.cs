namespace FinanceManager.Components.Models;

public sealed class NavMenuCacheSnapshot
{
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; set; } = CurrentSchemaVersion;
    public int UserId { get; set; }
    public DateTime FetchedAtUtc { get; set; }
    public List<NavMenuAccountCacheItem> Accounts { get; set; } = [];
    public bool DisplayAssetsLink { get; set; }
    public bool DisplayLiabilitiesLink { get; set; }
}

public sealed class NavMenuAccountCacheItem
{
    public int AccountId { get; set; }
    public string Name { get; set; } = string.Empty;
}