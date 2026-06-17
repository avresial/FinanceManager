using FinanceManager.Api.Helpers;
using FinanceManager.Domain.Entities.MoneyFlowModels;
using FinanceManager.Domain.FinancialAccounts.Shared.Services;
using FinanceManager.Domain.Identity.Services;
using FinanceManager.Domain.Insights.Entities;
using FinanceManager.Domain.Insights.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinanceManager.Api.Controllers;

[Route("api/[controller]")]
[Authorize]
[ApiController]
[Tags("Financial Analysis")]
public class DiversificationController(IDiversificationService diversificationService) : ControllerBase
{
    [HttpGet("{userId:int}/{asOfDate:DateTime}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(DiversificationScore))]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetDiversificationScore(int userId, DateTime asOfDate, CancellationToken cancellationToken = default)
    {
        if (!ApiAuthenticationHelper.IsAuthenticatedUser(User, userId))
            return Forbid();

        return Ok(await diversificationService.GetDiversificationScore(userId, asOfDate));
    }

    [HttpGet("{userId:int}/{asOfDate:DateTime}/breakdown")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(DiversificationBreakdown))]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetDiversificationBreakdown(int userId, DateTime asOfDate, CancellationToken cancellationToken = default)
    {
        if (!ApiAuthenticationHelper.IsAuthenticatedUser(User, userId))
            return Forbid();

        return Ok(await diversificationService.GetDiversificationBreakdown(userId, asOfDate, cancellationToken));
    }
}