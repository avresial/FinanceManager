using FinanceManager.Api.Shared.Helpers;
using FinanceManager.Domain.Dashboard.Dtos;
using FinanceManager.Domain.Dashboard.Services;
using FinanceManager.Domain.FinancialAccounts.Shared.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using System.ComponentModel.DataAnnotations;

namespace FinanceManager.Api.Features.Dashboard.Controllers;

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
    public async Task<IActionResult> Get(int userId, [FromQuery, Range(1, 100)] int count = 10, CancellationToken cancellationToken = default)
    {
        if (!ApiAuthenticationHelper.IsAuthenticatedUser(User, userId))
            return Forbid();

        return Ok(await transactionLogService.GetLastTransactions(userId, count, cancellationToken));
    }

    [HttpGet("Page/{userId:int}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(TransactionLogPage))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Page(int userId,
        [FromQuery, BindRequired] DateTime start, [FromQuery, BindRequired] DateTime end,
        [FromQuery] int pageSize = 25, [FromQuery] DateTime? cursorDate = null,
        [FromQuery] AccountType? cursorAccountType = null, [FromQuery] long? cursorEntryId = null,
        CancellationToken cancellationToken = default)
    {
        if (!ApiAuthenticationHelper.IsAuthenticatedUser(User, userId))
            return Forbid();

        if (pageSize is < 1 or > 100)
            ModelState.AddModelError(nameof(pageSize), "Page size must be between 1 and 100.");
        if (start > end)
            ModelState.AddModelError(nameof(start), "Start must be on or before end.");
        var hasCursor = cursorDate.HasValue || cursorAccountType.HasValue || cursorEntryId.HasValue;
        if (hasCursor && !(cursorDate.HasValue && cursorAccountType.HasValue && cursorEntryId.HasValue))
            ModelState.AddModelError(nameof(cursorDate), "Supply all cursor fields together.");
        if (cursorAccountType is AccountType type && !Enum.IsDefined(type))
            ModelState.AddModelError(nameof(cursorAccountType), "Unsupported account type.");
        if (cursorEntryId is <= 0)
            ModelState.AddModelError(nameof(cursorEntryId), "Cursor entry ID must be positive.");
        if (!ModelState.IsValid)
            return ValidationProblem(statusCode: StatusCodes.Status400BadRequest, modelStateDictionary: ModelState);

        var cursor = hasCursor ? new TransactionLogCursor(cursorDate!.Value, cursorAccountType!.Value, cursorEntryId!.Value) : null;
        return Ok(await transactionLogService.GetPage(userId, new TransactionLogQuery(start, end, pageSize, cursor), cancellationToken));
    }
}