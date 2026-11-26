using FinanceManager.Api.Shared.Helpers;
using FinanceManager.Domain.MoneyFlow.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinanceManager.Api.Features.MoneyFlow.Controllers;

[Authorize]
[Route("api/[controller]")]
[ApiController]
[Tags("Financial Analysis")]
public class UnconvertedCurrenciesController(IUnconvertedCurrencyService unconvertedCurrencyService) : ControllerBase
{
    /// <summary>The codes of the currencies, other than the display currency, that net worth and the distributions add up without converting.</summary>
    [HttpGet("{userId:int}/{currencyId:int}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(List<string>))]
    public async Task<IActionResult> Get(int userId, int currencyId, CancellationToken cancellationToken = default) =>
        ApiAuthenticationHelper.IsAuthenticatedUser(User, userId) ? Ok(await unconvertedCurrencyService.GetCodes(userId, currencyId, cancellationToken)) : Forbid();
}