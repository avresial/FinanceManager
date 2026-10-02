using FinanceManager.Api.Shared.Helpers;
using FinanceManager.Application.Identity.GiftCodes;
using FinanceManager.Domain.Identity.GiftCodes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinanceManager.Api.Features.Administration.Controllers;

[Route("api/admin/gift-codes")]
[Authorize(Roles = "Admin")]
[ApiController]
[Tags("Admin - Gift Codes")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public class AdminGiftCodesController(GiftCodeService service) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(int offset = 0, int count = 50, CancellationToken cancellationToken = default)
    {
        if (offset < 0 || count is < 1 or > 100) return BadRequest("Invalid page size or offset.");
        return Ok(await service.List(offset, count, cancellationToken));
    }

    [HttpPost]
    public async Task<IActionResult> Generate(GenerateGiftCode request, CancellationToken cancellationToken = default)
    {
        try { return Ok(await service.Generate(request, ApiAuthenticationHelper.GetUserId(User), cancellationToken)); }
        catch (ArgumentException ex) { return BadRequest(ex.Message); }
    }

    [HttpPost("{id:long}/revoke")]
    public async Task<IActionResult> Revoke(long id, CancellationToken cancellationToken = default) =>
        await service.Revoke(id, ApiAuthenticationHelper.GetUserId(User), cancellationToken)
            ? NoContent() : Conflict("Only an active code can be revoked.");
}