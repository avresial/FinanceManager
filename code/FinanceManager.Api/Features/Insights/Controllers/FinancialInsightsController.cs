using FinanceManager.Api.Features.Insights.Services;
using FinanceManager.Api.Shared.Helpers;
using FinanceManager.Application.Insights.Generation;
using FinanceManager.Domain.Identity.Entities;
using FinanceManager.Domain.Identity.Repositories;
using FinanceManager.Domain.Insights.Entities;
using FinanceManager.Domain.Insights.Repositories;
using FinanceManager.Domain.Shared.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinanceManager.Api.Features.Insights.Controllers;

[Authorize]
[Route("api/[controller]")]
[ApiController]
[Tags("Financial Insights")]
public class FinancialInsightsController(
    IFinancialInsightsRepository financialInsightsRepository,
    IFinancialInsightsAiGenerator financialInsightsAiGenerator,
    IInsightsGenerationChannel insightsGenerationChannel,
    IDateTimeProvider dateTimeProvider,
    ILogger<FinancialInsightsController> logger) : ControllerBase
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

        // Generation used to be triggered by an explicit login only, so a session kept alive by refresh tokens
        // could show the same insights for weeks. Reading them is the usage signal: once the newest one has aged
        // out, queue a regeneration for the background worker rather than making this response wait for the model.
        // Account-scoped reads are left out — generation produces insights for the whole user, so it belongs to
        // the unfiltered view that can actually tell whether the user's newest insight is stale.
        if (accountId is null && InsightsFreshness.IsStale(result.FirstOrDefault()?.CreatedAt, dateTimeProvider.UtcNow))
            await QueueRegeneration(userId, cancellationToken);

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

    /// <summary>
    /// Asks for a background regeneration without letting it affect the read: queueing is best-effort, so a full
    /// queue or a cancelled request still returns the insights the caller already has.
    /// </summary>
    private async Task QueueRegeneration(int userId, CancellationToken cancellationToken)
    {
        try
        {
            if (await insightsGenerationChannel.QueueUser(userId, cancellationToken))
                logger.LogDebug("Queued insights regeneration for user {UserId}; stored insights are stale.", userId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error occurred while queueing user {UserId} for insights generation", userId);
        }
    }
}