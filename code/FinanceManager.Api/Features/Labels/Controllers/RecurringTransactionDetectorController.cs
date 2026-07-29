using FinanceManager.Api.Shared.Helpers;
using FinanceManager.Domain.FinancialAccounts.Shared.Services;
using FinanceManager.Domain.Identity.Services;
using FinanceManager.Domain.Labels.Commands;
using FinanceManager.Domain.Labels.Entities;
using FinanceManager.Domain.Labels.Services;
using FinanceManager.Domain.MoneyFlow.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinanceManager.Api.Features.Labels.Controllers;

[Authorize]
[Route("api/[controller]")]
[ApiController]
[Tags("Financial Analysis")]
public class RecurringTransactionDetectorController(IRecurringTransactionDetectorService recurringTransactionDetectorService) : ControllerBase
{
    [HttpGet("Get/{userId:int}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(List<RecurringTransactionResult>))]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Get(int userId, CancellationToken cancellationToken = default)
    {
        if (!ApiAuthenticationHelper.IsAuthenticatedUser(User, userId))
            return Forbid();

        return Ok(await recurringTransactionDetectorService.GetRecurringTransactions(userId, cancellationToken));
    }

    [HttpPut("{userId:int}/{patternId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(
        int userId,
        Guid patternId,
        [FromBody] UpdateRecurringSubscription command,
        CancellationToken cancellationToken = default)
    {
        if (!ApiAuthenticationHelper.IsAuthenticatedUser(User, userId))
            return Forbid();

        return await recurringTransactionDetectorService.UpdateSubscription(
            userId,
            patternId,
            command,
            cancellationToken)
            ? NoContent()
            : NotFound();
    }
}