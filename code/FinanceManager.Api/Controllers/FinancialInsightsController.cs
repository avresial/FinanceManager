using FinanceManager.Api.Helpers;
using FinanceManager.Application.Services.FinancialInsights;
using FinanceManager.Domain.Entities.Users;
using FinanceManager.Domain.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinanceManager.Api.Controllers;

[Authorize]
[Route("api/[controller]")]
[ApiController]
[Tags("Financial Insights")]
public class FinancialInsightsController(
    IFinancialInsightsRepository financialInsightsRepository,
    IFinancialInsightsAiGenerator financialInsightsAiGenerator) : ControllerBase
{
    [HttpGet("get-latest")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(List<FinancialInsight>))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetLatest([FromQuery] int count = 3, [FromQuery] int? accountId = null, CancellationToken cancellationToken = default)
    {
        if (count <= 0)
            return BadRequest("Count must be greater than zero.");

        var userId = ApiAuthenticationHelper.GetUserId(User);
        var result = await financialInsightsRepository.GetLatestByUser(userId, count, accountId, cancellationToken);
        return Ok(result);
    }

    [HttpPost("generate")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(List<FinancialInsight>))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Generate([FromQuery] int count = 3, [FromQuery] int? accountId = null, CancellationToken cancellationToken = default)
    {
        if (count <= 0)
            return BadRequest("Count must be greater than zero.");

        var userId = ApiAuthenticationHelper.GetUserId(User);
        var insights = await financialInsightsAiGenerator.GenerateInsights(userId, accountId, count, cancellationToken);
        if (insights.Count == 0)
            return Ok(insights);

        await financialInsightsRepository.AddRange(insights, cancellationToken);
        return Ok(insights);
    }
}