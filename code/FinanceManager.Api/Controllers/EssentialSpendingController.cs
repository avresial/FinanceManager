using FinanceManager.Domain.Entities.MoneyFlowModels;
using FinanceManager.Domain.Repositories;
using FinanceManager.Domain.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FinanceManager.Api.Controllers;

[Authorize]
[Route("api/[controller]")]
[ApiController]
[Tags("Financial Analysis")]
public class EssentialSpendingController(IEssentialSpendingService essentialSpendingService, ICurrencyRepository currencyRepository) : ControllerBase
{
    [HttpGet("GetEssentialSpending/{userId:int}/{currencyId:int}/{start:DateTime}/{end:DateTime}/{step:long?}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(List<TimeSeriesModel>))]
    public async Task<IActionResult> GetEssentialSpending(int userId, int currencyId, DateTime start, DateTime end, long? step = null, [FromQuery] List<int>? accountIds = null, CancellationToken cancellationToken = default)
    {
        var currency = await currencyRepository.GetCurrencies(cancellationToken).SingleAsync(x => x.Id == currencyId, cancellationToken);
        return Ok(accountIds is { Count: > 0 }
            ? await essentialSpendingService.GetEssentialSpending(userId, currency, start, end, accountIds)
            : await essentialSpendingService.GetEssentialSpending(userId, currency, start, end));
    }
}