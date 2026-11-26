using FinanceManager.Api.Shared.Helpers;
using FinanceManager.Domain.Currencies.Repositories;
using FinanceManager.Domain.Identity.Repositories;
using FinanceManager.Domain.Identity.Services;
using FinanceManager.Domain.MoneyFlow.Entities;
using FinanceManager.Domain.MoneyFlow.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FinanceManager.Api.Features.MoneyFlow.Controllers;

[Authorize]
[Route("api/[controller]")]
[ApiController]
[Tags("Financial Analysis")]
public class EssentialSpendingController(IEssentialSpendingService essentialSpendingService, ICurrencyRepository currencyRepository) : ControllerBase
{
    [HttpGet("GetEssentialSpending/{userId:int}/{currencyId:int}/{start:DateTime}/{end:DateTime}/{step:long?}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(List<TimeSeriesModel>))]
    public async Task<IActionResult> GetEssentialSpending(int userId, int currencyId, DateTime start, DateTime end, long? step = null, CancellationToken cancellationToken = default)
    {
        if (!ApiAuthenticationHelper.IsAuthenticatedUser(User, userId)) return Forbid();

        var currency = await currencyRepository.GetCurrencies(cancellationToken).SingleAsync(x => x.Id == currencyId, cancellationToken);
        return Ok(await essentialSpendingService.GetEssentialSpending(userId, currency, start, end));
    }
}