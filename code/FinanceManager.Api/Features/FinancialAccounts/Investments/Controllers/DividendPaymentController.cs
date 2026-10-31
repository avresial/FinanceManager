using FinanceManager.Api.Shared.Helpers;
using FinanceManager.Application.FinancialAccounts.Investments.Dividends;
using FinanceManager.Domain.FinancialAccounts.Investments.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinanceManager.Api.Features.FinancialAccounts.Investments.Controllers;

[ApiController]
[Authorize]
[Route("api/accounts/{accountId:int}/dividends")]
[Tags("Investment Dividends")]
[ProducesResponseType(StatusCodes.Status400BadRequest)]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
[ProducesResponseType(StatusCodes.Status403Forbidden)]
[ProducesResponseType(StatusCodes.Status404NotFound)]
public class DividendPaymentController(DividendPaymentService service) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<DividendPaymentPage>(StatusCodes.Status200OK)]
    public Task<IActionResult> History(int accountId, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default) =>
        Execute(async () => Ok(await service.GetHistory(ApiAuthenticationHelper.GetUserId(User), accountId, page, pageSize, ct)));

    [HttpGet("holdings")]
    [ProducesResponseType<IReadOnlyList<DividendHoldingOption>>(StatusCodes.Status200OK)]
    public Task<IActionResult> Holdings(int accountId, CancellationToken ct) =>
        Execute(async () => Ok(await service.GetHoldings(ApiAuthenticationHelper.GetUserId(User), accountId, ct)));

    [HttpPost]
    [ProducesResponseType<long>(StatusCodes.Status200OK)]
    public Task<IActionResult> Add(int accountId, DividendPaymentRequest request, CancellationToken ct) =>
        Execute(async () => Ok(await service.Save(ApiAuthenticationHelper.GetUserId(User), accountId, null, request, ct)));

    [HttpPut("{id:long}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public Task<IActionResult> Update(int accountId, long id, DividendPaymentRequest request, CancellationToken ct) =>
        Execute(async () => { await service.Save(ApiAuthenticationHelper.GetUserId(User), accountId, id, request, ct); return NoContent(); });

    [HttpDelete("{id:long}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public Task<IActionResult> Delete(int accountId, long id, CancellationToken ct) =>
        Execute(async () => { await service.Delete(ApiAuthenticationHelper.GetUserId(User), accountId, id, ct); return NoContent(); });

    private async Task<IActionResult> Execute(Func<Task<IActionResult>> action)
    {
        try { return await action(); }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (KeyNotFoundException ex) { return NotFound(ex.Message); }
        catch (ArgumentException ex) { return BadRequest(ex.Message); }
    }
}