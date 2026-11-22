using FinanceManager.Api.Shared.Helpers;
using FinanceManager.Domain.Dashboard.Dtos;
using FinanceManager.Domain.Dashboard.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Globalization;

namespace FinanceManager.Api.Features.Dashboard.Controllers;

[Authorize]
[Route("api/[controller]")]
[ApiController]
[Tags("Financial Analysis")]
public class DashboardController(IDashboardQueryService dashboardQueryService) : ControllerBase
{
    private const string _dateOnlyFormat = "yyyy-MM-dd";
    private static readonly string[] _timestampFormats = ["yyyy-MM-dd'T'HH:mm:ss.FFFFFFFK", "yyyy-MM-dd'T'HH:mmK"];

    /// <summary>
    /// Returns the dashboard first-paint read model in a single response so the
    /// frontend can initialize dashboard state once instead of fanning out to the
    /// individual card-level endpoints.
    /// </summary>
    /// <param name="start">A UTC calendar day (<c>yyyy-MM-dd</c>, from midnight) or an ISO 8601 timestamp.</param>
    /// <param name="end">A UTC calendar day (<c>yyyy-MM-dd</c>, inclusive through its last tick) or an ISO 8601 timestamp.</param>
    [HttpGet("overview")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(DashboardOverviewDto))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetOverview([FromQuery] int userId, [FromQuery] int currencyId, [FromQuery] string start, [FromQuery] string end, CancellationToken cancellationToken = default)
    {
        if (!ApiAuthenticationHelper.IsAuthenticatedUser(User, userId)) return Forbid();

        if (!TryParseBoundary(start, endOfDay: false, out var startUtc))
            ModelState.AddModelError(nameof(start), $"'{start}' is not a valid {_dateOnlyFormat} date or ISO 8601 timestamp.");
        if (!TryParseBoundary(end, endOfDay: true, out var endUtc))
            ModelState.AddModelError(nameof(end), $"'{end}' is not a valid {_dateOnlyFormat} date or ISO 8601 timestamp.");
        if (ModelState.IsValid && startUtc > endUtc)
            ModelState.AddModelError(nameof(end), "The end of the range must not be before its start.");
        if (!ModelState.IsValid) return ValidationProblem(ModelState);

        var overview = await dashboardQueryService.GetOverview(userId, currencyId, startUtc, endUtc, cancellationToken);
        return overview is null ? NotFound() : Ok(overview);
    }

    // A bare date names a whole UTC day: the start begins at midnight and the end is inclusive through
    // the day's last tick. Timestamps keep exact intraday callers working; offsets (or a missing one,
    // read as UTC) are normalized to UTC. Values that overflow the DateTime range after shifting fail.
    private static bool TryParseBoundary(string value, bool endOfDay, out DateTime utc)
    {
        if (DateOnly.TryParseExact(value, _dateOnlyFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var day))
        {
            utc = day.ToDateTime(endOfDay ? TimeOnly.MaxValue : TimeOnly.MinValue, DateTimeKind.Utc);
            return true;
        }

        if (DateTimeOffset.TryParseExact(value, _timestampFormats, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var timestamp))
        {
            utc = timestamp.UtcDateTime;
            return true;
        }

        utc = default;
        return false;
    }
}