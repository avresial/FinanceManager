using FinanceManager.Application.Shared.Maintenance;
using FinanceManager.Domain.Administration.Logging;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace FinanceManager.Api.Features.Maintenance.Controllers;

[Route("api/maintenance/logs")]
[ApiController]
[Tags("Maintenance")]
[EnableRateLimiting(RateLimitingServiceCollectionExtension.AuthPolicy)]
public class MaintenanceLogsController(
    IMaintenanceKeyService maintenanceKeyService,
    ILogEntryRepository repository,
    ILogger<MaintenanceLogsController> logger) : ControllerBase
{
    private const int _defaultTake = 25;
    private const int _maxTake = 200;
    private const int _maxSearchLength = 256;

    [AllowAnonymous]
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(PagedLogEntriesDto))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(
        [FromQuery] int skip = 0,
        [FromQuery] int take = _defaultTake,
        [FromQuery] string? level = null,
        [FromQuery] DateTime? fromUtc = null,
        [FromQuery] DateTime? toUtc = null,
        [FromQuery] string? search = null,
        CancellationToken cancellationToken = default)
    {
        if (!await maintenanceKeyService.IsConfiguredAsync(cancellationToken))
            return NotFound();

        if (!Request.Headers.TryGetValue(PriceBackfillController.ApiKeyHeader, out var providedKey) ||
            !await maintenanceKeyService.ValidateAsync(providedKey.ToString(), cancellationToken))
        {
            logger.LogWarning("Maintenance log read rejected: missing or invalid maintenance key.");
            return Unauthorized();
        }

        if (skip < 0) return BadRequest("skip must be non-negative.");
        if (take <= 0) return BadRequest("take must be greater than zero.");
        if (take > _maxTake) return BadRequest($"take must be {_maxTake} or less.");
        if (fromUtc > toUtc) return BadRequest("fromUtc must not be later than toUtc.");
        if (search?.Length > _maxSearchLength)
            return BadRequest($"search must be {_maxSearchLength} characters or fewer.");
        if (!TryParseLevel(level, out var levels)) return BadRequest("level is not supported.");

        var (items, total) = await repository.GetPaged(
            skip,
            take,
            levels,
            fromUtc,
            toUtc,
            search,
            cancellationToken);

        return Ok(new PagedLogEntriesDto(items.Select(Map).ToList(), total));
    }

    private static bool TryParseLevel(string? level, out IReadOnlyCollection<LogSeverity>? levels)
    {
        levels = level?.ToLowerInvariant() switch
        {
            null or "" => null,
            "trace" => [LogSeverity.Trace],
            "debug" => [LogSeverity.Debug],
            "information" or "info" => [LogSeverity.Information],
            "warning" or "warn" => [LogSeverity.Warning],
            "error" => [LogSeverity.Error],
            "critical" => [LogSeverity.Critical],
            _ => null,
        };

        return string.IsNullOrWhiteSpace(level) ||
            level.Equals("trace", StringComparison.OrdinalIgnoreCase) ||
            level.Equals("debug", StringComparison.OrdinalIgnoreCase) ||
            level.Equals("information", StringComparison.OrdinalIgnoreCase) ||
            level.Equals("info", StringComparison.OrdinalIgnoreCase) ||
            level.Equals("warning", StringComparison.OrdinalIgnoreCase) ||
            level.Equals("warn", StringComparison.OrdinalIgnoreCase) ||
            level.Equals("error", StringComparison.OrdinalIgnoreCase) ||
            level.Equals("critical", StringComparison.OrdinalIgnoreCase);
    }

    private static LogEntryDto Map(LogEntry entry) =>
        new(
            entry.Id,
            entry.TimestampUtc,
            entry.Level,
            entry.Category,
            entry.Message,
            entry.Exception,
            entry.EventId,
            entry.EventName);
}