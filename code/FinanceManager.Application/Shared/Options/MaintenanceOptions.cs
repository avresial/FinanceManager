namespace FinanceManager.Application.Shared.Options;

public sealed class MaintenanceOptions
{
    public const string SectionName = "Maintenance";

    /// <summary>
    /// Shared secret required by the maintenance endpoints (e.g. the weekly price backfill trigger
    /// called from the scheduled GitHub Actions workflow). When empty the endpoints answer 404, so
    /// deployments that never configure a key expose no maintenance surface at all.
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;
}