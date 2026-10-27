using FinanceManager.Application.FinancialAccounts.Investments.Discovery;
using FinanceManager.Domain.Assets.Discovery;
using FinanceManager.Domain.Assets.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinanceManager.Api.Features.Assets.Controllers;

[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/admin/assets/discovery")]
[Tags("Admin - Assets")]
public class AdminAssetDiscoveryController(AdminAssetDiscoveryService service) : ControllerBase
{
    [HttpGet("search")]
    public Task<IActionResult> Search([FromQuery] string? query, CancellationToken ct) =>
        Execute(() => service.SearchAsync(query ?? string.Empty, ct));

    [HttpPost("draft")]
    public Task<IActionResult> Draft([FromBody] InstrumentDiscoveryResultDto instrument, CancellationToken ct) =>
        Execute(() => service.GetDraftAsync(instrument, ct));

    [HttpPost("listings")]
    public Task<IActionResult> Listings([FromBody] AssetDto asset, CancellationToken ct) =>
        Execute(() => service.FindListingsAsync(asset, ct));

    [HttpPost("symbols")]
    public Task<IActionResult> Symbols([FromBody] InstrumentDiscoveryResultDto instrument, [FromQuery] long? listingId, CancellationToken ct) =>
        Execute(() => service.ResolveSymbolsAsync(instrument, listingId, ct));

    [HttpPost("create")]
    public Task<IActionResult> Create([FromBody] AssetDto draft, CancellationToken ct) =>
        Execute(() => service.CreateAsync(draft, ct));

    [HttpPost("{assetId:long}/listings")]
    public Task<IActionResult> AddListing(long assetId, [FromBody] AssetListingDto draft, CancellationToken ct) =>
        Execute(() => service.AddListingAsync(assetId, draft, ct));

    private async Task<IActionResult> Execute<T>(Func<Task<T>> action)
    {
        try { return Ok(await action()); }
        catch (ArgumentException ex) { return BadRequest(ex.Message); }
        catch (KeyNotFoundException ex) { return NotFound(ex.Message); }
    }
}