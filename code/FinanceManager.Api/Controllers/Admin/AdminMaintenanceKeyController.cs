using FinanceManager.Application.Shared.Maintenance;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinanceManager.Api.Controllers.Admin;

/// <summary>Status of the maintenance key as shown to the admin (never the key itself).</summary>
public record MaintenanceKeyStatusResponse(bool HasKey, DateTimeOffset? CreatedAt);

/// <summary>Result of generating (or rolling) the key — the only time the plaintext is available.</summary>
public record GeneratedMaintenanceKeyResponse(string Key, DateTimeOffset CreatedAt);

[Route("api/admin/maintenance-key")]
[Authorize(Roles = "Admin")]
[ApiController]
[Tags("Admin - Maintenance Key")]
public class AdminMaintenanceKeyController(
    IMaintenanceKeyService maintenanceKeyService,
    ILogger<AdminMaintenanceKeyController> logger) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(MaintenanceKeyStatusResponse))]
    public async Task<IActionResult> GetStatus(CancellationToken cancellationToken)
    {
        var status = await maintenanceKeyService.GetStatusAsync(cancellationToken);
        return Ok(new MaintenanceKeyStatusResponse(status.HasKey, status.CreatedAt));
    }

    /// <summary>Generates a new key; when one already exists this rolls it — the old key stops working immediately.</summary>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(GeneratedMaintenanceKeyResponse))]
    public async Task<IActionResult> Generate(CancellationToken cancellationToken)
    {
        var key = await maintenanceKeyService.GenerateAsync(cancellationToken);
        logger.LogInformation("Maintenance API key generated/rolled by admin.");
        return Ok(new GeneratedMaintenanceKeyResponse(key, DateTimeOffset.UtcNow));
    }

    [HttpDelete]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Revoke(CancellationToken cancellationToken)
    {
        if (!await maintenanceKeyService.RevokeAsync(cancellationToken))
            return NotFound();

        logger.LogInformation("Maintenance API key revoked by admin.");
        return NoContent();
    }
}