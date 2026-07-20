using FinanceManager.Api.Services;
using FinanceManager.Application.Shared.Options;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using System.Security.Cryptography;
using System.Text;

namespace FinanceManager.Api.Controllers;

/// <summary>
/// External-scheduler entry point for the weekly closing-price backfill. The app runs on hosting
/// where an in-process timer cannot be trusted to be awake (free-tier App Service unloads when
/// idle), so a scheduled GitHub Actions workflow calls this endpoint instead — the request itself
/// wakes the app, the job is queued, and the worker finishes after the caller has gone.
/// </summary>
[Route("api/[controller]")]
[ApiController]
[Tags("Maintenance")]
[EnableRateLimiting(RateLimitingServiceCollectionExtension.AuthPolicy)]
public class PriceBackfillController(
    IOptions<MaintenanceOptions> maintenanceOptions,
    IPriceBackfillJobChannel channel,
    ILogger<PriceBackfillController> logger) : ControllerBase
{
    public const string ApiKeyHeader = "X-Maintenance-Key";
    private const int _backfillDays = 7;

    [AllowAnonymous]
    [HttpPost(Name = "TriggerPriceBackfill")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult Trigger()
    {
        var configuredKey = maintenanceOptions.Value.ApiKey;

        // 404 (not 401) when no key is configured so deployments that never opted into maintenance
        // endpoints are indistinguishable from ones where the route does not exist (same stance as
        // DevelopLoginController outside development).
        if (string.IsNullOrWhiteSpace(configuredKey))
            return NotFound();

        if (!Request.Headers.TryGetValue(ApiKeyHeader, out var providedKey) ||
            !FixedTimeEquals(configuredKey, providedKey.ToString()))
        {
            logger.LogWarning("Price backfill trigger rejected: missing or invalid maintenance key.");
            return Unauthorized();
        }

        var end = DateTime.UtcNow.Date;
        var start = end.AddDays(-_backfillDays);
        var queued = channel.TryQueueJob(new PriceBackfillJobRequest(start, end));

        logger.LogInformation(
            queued
                ? "Price backfill for {Start:yyyy-MM-dd}..{End:yyyy-MM-dd} queued."
                : "Price backfill trigger received while a run was already queued; collapsed into the pending run.",
            start, end);

        return Accepted(value: new { start, end, queued });
    }

    // Constant-time comparison so response timing cannot be used to guess the key byte by byte.
    private static bool FixedTimeEquals(string expected, string provided) =>
        CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(expected),
            Encoding.UTF8.GetBytes(provided));
}