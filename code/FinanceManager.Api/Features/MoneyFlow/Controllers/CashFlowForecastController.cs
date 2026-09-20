using FinanceManager.Api.Shared.Helpers;
using FinanceManager.Domain.FinancialAccounts.Currencies.Repositories;
using FinanceManager.Domain.Identity.Services;
using FinanceManager.Domain.MoneyFlow.Entities;
using FinanceManager.Domain.MoneyFlow.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinanceManager.Api.Features.MoneyFlow.Controllers;

[Authorize]
[Route("api/[controller]")]
[ApiController]
[Tags("Financial Analysis")]
public class CashFlowForecastController(
    ICashFlowForecastService cashFlowForecastService,
    ICurrencyRepository currencyRepository) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(CashFlowForecast))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(
        [FromQuery] int userId,
        [FromQuery] int currencyId,
        [FromQuery] int horizonDays = CashFlowForecastHorizons.NinetyDays,
        CancellationToken cancellationToken = default)
    {
        if (!ApiAuthenticationHelper.IsAuthenticatedUser(User, userId))
            return Forbid();
        if (!CashFlowForecastHorizons.IsSupported(horizonDays))
            return BadRequest("The forecast horizon must be 30, 60, or 90 days.");

        var currency = await currencyRepository.GetCurrency(currencyId, cancellationToken);
        if (currency is null)
            return NotFound("Currency not found.");

        return Ok(await cashFlowForecastService.GetForecast(userId, currency, horizonDays, cancellationToken));
    }
}