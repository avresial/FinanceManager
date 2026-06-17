using FinanceManager.Domain.Administration.Logging;
using FinanceManager.Domain.Enums;
using FinanceManager.Domain.FinancialAccounts.Shared.Dtos;
using FinanceManager.Domain.Identity.Repositories;
using FinanceManager.Domain.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinanceManager.Api.Controllers.Admin;

[Route("api/admin/logs")]
[Authorize(Roles = "Admin")]
[ApiController]
[Tags("Administration")]
public class AdminLogsController(ILogEntryRepository repository) : ControllerBase
{
    private static readonly IReadOnlyCollection<LogSeverity> _warningAndError =
        [LogSeverity.Warning, LogSeverity.Error, LogSeverity.Critical];

    [HttpGet("latest")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(List<LogEntryDto>))]
    public async Task<IActionResult> GetLatest([FromQuery] int count = 5, CancellationToken cancellationToken = default)
    {
        if (count <= 0) return BadRequest("count must be greater than zero.");
        if (count > 50) return BadRequest("count must be 50 or less.");

        var entries = await repository.GetLatest(count, _warningAndError, cancellationToken);
        return Ok(entries.Select(Map).ToList());
    }

    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(PagedLogEntriesDto))]
    public async Task<IActionResult> GetPaged(
        [FromQuery] int skip = 0,
        [FromQuery] int take = 25,
        [FromQuery] string? level = null,
        CancellationToken cancellationToken = default)
    {
        if (skip < 0) return BadRequest("skip must be non-negative.");
        if (take <= 0) return BadRequest("take must be greater than zero.");
        if (take > 200) return BadRequest("take must be 200 or less.");

        var levels = ParseLevels(level);
        var (items, total) = await repository.GetPaged(skip, take, levels, cancellationToken);
        return Ok(new PagedLogEntriesDto(items.Select(Map).ToList(), total));
    }

    private static IReadOnlyCollection<LogSeverity> ParseLevels(string? level) => level?.ToLowerInvariant() switch
    {
        "warning" => [LogSeverity.Warning],
        "error" => [LogSeverity.Error, LogSeverity.Critical],
        _ => _warningAndError,
    };

    private static LogEntryDto Map(LogEntry e) =>
        new(e.Id, e.TimestampUtc, e.Level, e.Category, e.Message, e.Exception, e.EventId, e.EventName);
}