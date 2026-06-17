using FinanceManager.Api.Helpers;
using FinanceManager.Domain.Dashboard.Dtos;
using FinanceManager.Domain.Dashboard.Services;
using FinanceManager.Domain.Identity.Services;
using FinanceManager.Domain.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinanceManager.Api.Controllers;

[Authorize]
[Route("api/[controller]")]
[ApiController]
[Tags("Financial Analysis")]
public class DashboardController(IDashboardQueryService dashboardQueryService) : ControllerBase
{
    /// <summary>
    /// Returns the dashboard first-paint read model in a single response so the
    /// frontend can initialize dashboard state once instead of fanning out to the
    /// individual card-level endpoints.
    /// </summary>
    [HttpGet("overview")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(DashboardOverviewDto))]
    public async Task<IActionResult> GetOverview([FromQuery] int userId, [FromQuery] int currencyId, [FromQuery] DateTime start, [FromQuery] DateTime end, CancellationToken cancellationToken = default)
    {
        if (!ApiAuthenticationHelper.IsAuthenticatedUser(User, userId)) return Forbid();

        var overview = await dashboardQueryService.GetOverview(userId, currencyId, start, end, cancellationToken);
        return overview is null ? NotFound() : Ok(overview);
    }
}