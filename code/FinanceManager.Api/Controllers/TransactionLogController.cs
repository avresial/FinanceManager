using FinanceManager.Api.Helpers;
using FinanceManager.Domain.Dashboard.Dtos;
using FinanceManager.Domain.Dashboard.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinanceManager.Api.Controllers;

[Authorize]
[Route("api/[controller]")]
[ApiController]
[Tags("Financial Analysis")]
public class TransactionLogController(ITransactionLogService transactionLogService) : ControllerBase
{
    /// <summary>
    /// Returns the user's most recent transactions across all of their accounts,
    /// regardless of account type, ordered newest first.
    /// </summary>
    [HttpGet("Get/{userId:int}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(List<TransactionLogEntryDto>))]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Get(int userId, [FromQuery] int count = 10, CancellationToken cancellationToken = default)
    {
        if (!ApiAuthenticationHelper.IsAuthenticatedUser(User, userId))
            return Forbid();

        return Ok(await transactionLogService.GetLastTransactions(userId, count, cancellationToken));
    }
}