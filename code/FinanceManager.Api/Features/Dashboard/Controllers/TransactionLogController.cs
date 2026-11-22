using FinanceManager.Api.Features.Dashboard.Extensions;
using FinanceManager.Api.Shared.Helpers;
using FinanceManager.Contracts.Dashboard;
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
    private const int _maxAccountIds = 100;

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
        [FromQuery] AccountType[]? accountTypes = null, [FromQuery] int[]? accountIds = null,
        [FromQuery] string? search = null,
        CancellationToken cancellationToken = default)
    {
        if (!ApiAuthenticationHelper.IsAuthenticatedUser(User, userId))
            return Forbid();
        if (BuildQuery(start, end, pageSize, cursorDate, cursorAccountType, cursorEntryId, accountTypes, accountIds, search) is not TransactionLogQuery query)
            return ValidationProblem(statusCode: StatusCodes.Status400BadRequest, modelStateDictionary: ModelState);

        return Ok(await transactionLogService.GetPage(userId, query, cancellationToken));
    }

    /// <summary>
    /// Same selection and cursor as <see cref="Page"/>, but each item is typed by account kind and carries the
    /// full entry, so the Transactions page can render it with the account page's row. <see cref="Page"/> stays
    /// for clients that still expect the compact shape.
    /// </summary>
    [HttpGet("ItemsPage/{userId:int}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(TransactionLogItemsPageDto))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ItemsPage(int userId,
        [FromQuery, BindRequired] DateTime start, [FromQuery, BindRequired] DateTime end,
        [FromQuery] int pageSize = 25, [FromQuery] DateTime? cursorDate = null,
        [FromQuery] AccountType? cursorAccountType = null, [FromQuery] long? cursorEntryId = null,
        [FromQuery] AccountType[]? accountTypes = null, [FromQuery] int[]? accountIds = null,
        [FromQuery] string? search = null,
        CancellationToken cancellationToken = default)
    {
        if (!ApiAuthenticationHelper.IsAuthenticatedUser(User, userId))
            return Forbid();
        if (BuildQuery(start, end, pageSize, cursorDate, cursorAccountType, cursorEntryId, accountTypes, accountIds, search) is not TransactionLogQuery query)
            return ValidationProblem(statusCode: StatusCodes.Status400BadRequest, modelStateDictionary: ModelState);

        var page = await transactionLogService.GetItemsPage(userId, query, cancellationToken);
        return Ok(page.ToDto());
    }

    // Records every problem in ModelState and returns null when the query is invalid.
    private TransactionLogQuery? BuildQuery(DateTime start, DateTime end, int pageSize, DateTime? cursorDate,
        AccountType? cursorAccountType, long? cursorEntryId, AccountType[]? accountTypes, int[]? accountIds, string? search)
    {
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
        if (accountTypes is not null && accountTypes.Any(t => !Enum.IsDefined(t)))
            ModelState.AddModelError(nameof(accountTypes), "Unsupported account type.");
        if (accountIds is { Length: > _maxAccountIds })
            ModelState.AddModelError(nameof(accountIds), $"At most {_maxAccountIds} account IDs are allowed.");
        if (accountIds is not null && accountIds.Any(id => id <= 0))
            ModelState.AddModelError(nameof(accountIds), "Account IDs must be positive.");
        if (!ModelState.IsValid)
            return null;

        var cursor = hasCursor ? new TransactionLogCursor(cursorDate!.Value, cursorAccountType!.Value, cursorEntryId!.Value) : null;
        return new TransactionLogQuery(start, end, pageSize, cursor, accountTypes, accountIds, search);
    }
}