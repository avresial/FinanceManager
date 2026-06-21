using FinanceManager.Domain.Assets.Entities;

namespace FinanceManager.Domain.Assets.Dtos;

/// <summary>
/// Data transfer object for an <see cref="Asset"/> used by the admin management API and UI.
/// Flat (no entity back-references) so it serialises cleanly and binds to Blazor forms.
/// Doubles as both request (create/update) and response shape; <see cref="Id"/> is ignored on create.
/// A mutable class (not a record) is used deliberately: the admin edit form binds these properties
/// two-way with <c>@bind-Value</c>, which requires settable (not init-only) members.
/// </summary>
public class AssetDto
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public AssetType Type { get; set; }
    public string? Isin { get; set; }
    public string? ShareClassFigi { get; set; }
    public string? CompositeFigi { get; set; }
    public string? Issuer { get; set; }
    public string? Domicile { get; set; }
    public string? BaseCurrency { get; set; }
    public DistributionPolicy? DistributionPolicy { get; set; }
    public string? BenchmarkIndex { get; set; }
    public ReplicationMethod? ReplicationMethod { get; set; }
    public decimal? TotalExpenseRatio { get; set; }
    public bool? IsUcits { get; set; }
    public DateOnly? InceptionDate { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public List<AssetListingDto> Listings { get; set; } = [];
}