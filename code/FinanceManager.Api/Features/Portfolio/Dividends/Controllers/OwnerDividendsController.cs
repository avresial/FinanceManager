using FinanceManager.Api.Shared.Helpers;
using FinanceManager.Application.FinancialAccounts.Investments.Dividends;
using FinanceManager.Domain.FinancialAccounts.Investments.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinanceManager.Api.Features.Portfolio.Dividends.Controllers;

/// <summary>The caller's dividend payments across all brokers. New payments are not linked to an account.</summary>
[ApiController]
[Authorize]
[Route("api/dividends")]
[Tags("Dividends")]
[ProducesResponseType(StatusCodes.Status400BadRequest)]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
[ProducesResponseType(StatusCodes.Status404NotFound)]
public class OwnerDividendsController(DividendPaymentService service) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<DividendPaymentPage>(StatusCodes.Status200OK)]
    public Task<IActionResult> History([FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default) =>
        Execute(async ownerId => Ok(await service.GetHistory(ownerId, null, page, pageSize, ct)));

    [HttpGet("holdings")]
    [ProducesResponseType<IReadOnlyList<DividendHoldingOption>>(StatusCodes.Status200OK)]
    public Task<IActionResult> Holdings(CancellationToken ct) =>
        Execute(async ownerId => Ok(await service.GetHoldings(ownerId, null, ct)));

    [HttpPost]
    [ProducesResponseType<long>(StatusCodes.Status200OK)]
    public Task<IActionResult> Add(DividendPaymentRequest request, CancellationToken ct) =>
        Execute(async ownerId => Ok(await service.Save(ownerId, null, null, request, ct)));

    [HttpPut("{id:long}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public Task<IActionResult> Update(long id, DividendPaymentRequest request, CancellationToken ct) =>
        Execute(async ownerId => { await service.Save(ownerId, null, id, request, ct); return NoContent(); });

    [HttpDelete("{id:long}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public Task<IActionResult> Delete(long id, CancellationToken ct) =>
        Execute(async ownerId => { await service.Delete(ownerId, null, id, ct); return NoContent(); });

    private async Task<IActionResult> Execute(Func<int, Task<IActionResult>> action)
    {
        if (!ApiAuthenticationHelper.TryGetOwnerId(User, out var ownerId)) return Forbid();
        try { return await action(ownerId); }
        catch (KeyNotFoundException ex) { return NotFound(ex.Message); }
        catch (ArgumentException ex) { return BadRequest(ex.Message); }
    }
}