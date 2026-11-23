using FinanceManager.Api.Features.Dashboard.Extensions;
using FinanceManager.Contracts.Dashboard;
using FinanceManager.Domain.Dashboard.Dtos;
using FinanceManager.Domain.Dashboard.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;

namespace FinanceManager.Api.Features.Dashboard.Controllers;

[Authorize]
[Route("api/[controller]")]
[ApiController]
[Tags("Financial Analysis")]
public class TransactionLogController(ITransactionLogService transactionLogService) : ControllerBase
{
    private const int _maxCurrencies = 100;

    /// <summary>
    /// Returns the user's most recent cash, bond and investment transactions
    /// as compact summaries, ordered newest first.
    /// </summary>
    [HttpGet("Get/{userId:int}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(List<TransactionLogEntryDto>))]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Get(int userId, [FromQuery, Range(1, 100)] int count = 10, CancellationToken cancellationToken = default)
    {
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var ownerId) || ownerId == 0 || ownerId != userId)
            return Forbid();

        return Ok(await transactionLogService.GetLastTransactions(ownerId, count, cancellationToken));
    }

    [HttpGet("Currencies/{userId:int}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(List<string>))]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Currencies(int userId, CancellationToken cancellationToken = default)
    {
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var ownerId) || ownerId == 0 || ownerId != userId)
            return Forbid();

        return Ok(await transactionLogService.GetCurrencies(ownerId, cancellationToken));
    }

    [HttpGet("ItemsPage/{userId:int}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(TransactionLogItemsPageDto))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ItemsPage(int userId,
        [FromQuery, BindRequired] DateTime start, [FromQuery, BindRequired] DateTime end,
        [FromQuery] int pageSize = 25, [FromQuery] DateTime? cursorDate = null,
        [FromQuery] TransactionKind? cursorKind = null, [FromQuery] long? cursorEntryId = null,
        [FromQuery] TransactionKind[]? kinds = null, [FromQuery] string[]? currencies = null,
        [FromQuery] string? search = null,
        CancellationToken cancellationToken = default)
    {
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var ownerId) || ownerId == 0 || ownerId != userId)
            return Forbid();
        if (BuildQuery(start, end, pageSize, cursorDate, cursorKind, cursorEntryId, kinds, currencies, search) is not TransactionLogQuery query)
            return ValidationProblem(statusCode: StatusCodes.Status400BadRequest, modelStateDictionary: ModelState);

        var page = await transactionLogService.GetItemsPage(ownerId, query, cancellationToken);
        return Ok(page.ToDto());
    }

    // Records every problem in ModelState and returns null when the query is invalid.
    private TransactionLogQuery? BuildQuery(DateTime start, DateTime end, int pageSize, DateTime? cursorDate,
        TransactionKind? cursorKind, long? cursorEntryId, TransactionKind[]? kinds, string[]? currencies, string? search)
    {
        if (pageSize is < 1 or > 100)
            ModelState.AddModelError(nameof(pageSize), "Page size must be between 1 and 100.");
        if (TransactionLogQuery.AsUtc(start) > TransactionLogQuery.AsUtc(end))
            ModelState.AddModelError(nameof(start), "Start must be on or before end.");
        var hasCursor = cursorDate.HasValue || cursorKind.HasValue || cursorEntryId.HasValue;
        if (hasCursor && !(cursorDate.HasValue && cursorKind.HasValue && cursorEntryId.HasValue))
            ModelState.AddModelError(nameof(cursorDate), "Supply all cursor fields together.");
        if (cursorKind is TransactionKind type && !Enum.IsDefined(type))
            ModelState.AddModelError(nameof(cursorKind), "Unsupported transaction kind.");
        if (cursorEntryId is <= 0)
            ModelState.AddModelError(nameof(cursorEntryId), "Cursor entry ID must be positive.");
        if (kinds is not null && kinds.Any(t => !Enum.IsDefined(t)))
            ModelState.AddModelError(nameof(kinds), "Unsupported transaction kind.");
        if (currencies is { Length: > _maxCurrencies })
            ModelState.AddModelError(nameof(currencies), $"At most {_maxCurrencies} currencies are allowed.");
        if (currencies is not null && currencies.Any(code => code?.Trim() is not { Length: 3 } trimmed || !trimmed.All(char.IsAsciiLetter)))
            ModelState.AddModelError(nameof(currencies), "Currency codes must contain three ASCII letters.");
        if (!ModelState.IsValid)
            return null;

        var cursor = hasCursor ? new TransactionLogCursor(cursorDate!.Value, cursorKind!.Value, cursorEntryId!.Value) : null;
        return new TransactionLogQuery(start, end, pageSize, cursor, kinds, currencies?.Select(code => code.Trim().ToUpperInvariant()).Distinct().ToArray(), search);
    }
}