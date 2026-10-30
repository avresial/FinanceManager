using FinanceManager.Application.FinancialAccounts.Investments.Discovery;
using FinanceManager.Domain.Assets.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinanceManager.Api.Features.Assets.Controllers;

[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/admin/assets/incomplete")]
public class IncompleteAssetsController(IncompleteAssetQueueService service) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] string? search = null, CancellationToken ct = default)
    {
        try { return Ok(await service.GetAsync(page, pageSize, search, ct)); }
        catch (ArgumentException ex) { return BadRequest(ex.Message); }
    }

    [HttpPost("listings/{listingId:long}/symbols")]
    public async Task<IActionResult> SaveSymbol(long listingId, [FromBody] MarketDataSymbolDto symbol, CancellationToken ct)
    {
        try { return Ok(await service.ValidateAndSaveSymbolAsync(listingId, symbol, ct)); }
        catch (ArgumentException ex) { return BadRequest(ex.Message); }
        catch (KeyNotFoundException ex) { return NotFound(ex.Message); }
    }
}