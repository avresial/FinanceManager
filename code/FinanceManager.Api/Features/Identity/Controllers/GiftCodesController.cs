using FinanceManager.Api.Shared.Helpers;
using FinanceManager.Application.Identity.GiftCodes;
using FinanceManager.Domain.Identity.GiftCodes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace FinanceManager.Api.Features.Identity.Controllers;

[Route("api/gift-codes")]
[Authorize(Roles = "Admin, User")]
[ApiController]
[Tags("Gift Codes")]
public class GiftCodesController(GiftCodeService service) : ControllerBase
{
    [HttpPost("redeem")]
    [EnableRateLimiting(RateLimitingServiceCollectionExtension.AuthPolicy)]
    public async Task<IActionResult> Redeem(RedeemGiftCode request, CancellationToken cancellationToken = default)
    {
        var result = await service.Redeem(request.Code, ApiAuthenticationHelper.GetUserId(User), cancellationToken);
        return result switch
        {
            GiftCodeRedemptionResult.Redeemed => NoContent(),
            GiftCodeRedemptionResult.NotAnUpgrade => BadRequest("This code does not upgrade your current tier. It has not been used."),
            GiftCodeRedemptionResult.UserNotFound => NotFound("Your account could not be found."),
            _ => BadRequest("This gift code is invalid, already redeemed, or revoked.")
        };
    }
}