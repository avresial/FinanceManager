using FinanceManager.Domain.Repositories;
using FinanceManager.Domain.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinanceManager.Api.Controllers;

[Authorize]
[Route("api/[controller]")]
[ApiController]
public class MoneyFlowController(IMoneyFlowService moneyFlowService, ICurrencyRepository currencyRepository) : ControllerBase
{
    [HttpGet("GetNetWorth/{userId:int}/{currencyId:int}/{date:DateTime}")]
    public async Task<IActionResult> GetNetWorth(int userId, int currencyId, DateTime date, CancellationToken cancellationToken = default) =>
        Ok(await moneyFlowService.GetNetWorth(userId, await currencyRepository.GetCurrencies().SingleAsync(x => x.Id == currencyId, cancellationToken), date));

    [HttpGet("GetNetWorth/{userId:int}/{currencyId:int}/{start:DateTime}/{end:DateTime}")]
    public async Task<IActionResult> GetNetWorth(int userId, int currencyId, DateTime start, DateTime end, CancellationToken cancellationToken = default) =>
        Ok(await moneyFlowService.GetNetWorth(userId, await currencyRepository.GetCurrencies().SingleAsync(x => x.Id == currencyId, cancellationToken), start, end));

    [HttpGet("GetIncome/{userId:int}/{currencyId:int}/{start:DateTime}/{end:DateTime}/{step:long?}")]
    public async Task<IActionResult> GetIncome(int userId, int currencyId, DateTime start, DateTime end, long? step = null, CancellationToken cancellationToken = default) =>
        Ok(await moneyFlowService.GetIncome(userId, await currencyRepository.GetCurrencies().SingleAsync(x => x.Id == currencyId, cancellationToken), start, end));

    [HttpGet("GetSpending/{userId:int}/{currencyId:int}/{start:DateTime}/{end:DateTime}/{step:long?}")]
    public async Task<IActionResult> GetSpending(int userId, int currencyId, DateTime start, DateTime end, long? step = null, CancellationToken cancellationToken = default) =>
        Ok(await moneyFlowService.GetSpending(userId, await currencyRepository.GetCurrencies().SingleAsync(x => x.Id == currencyId, cancellationToken), start, end));

    [HttpGet("GetLabelsValue")]
    public async Task<IActionResult> GetLabelsValue([FromQuery] int userId, [FromQuery] DateTime start, [FromQuery] DateTime end, [FromQuery] long? step = null, CancellationToken cancellationToken = default) =>
        Ok(await moneyFlowService.GetLabelsValue(userId, start, end));

    [HttpGet("GetInvestmentRate")]
    public async Task<IActionResult> GetInvestmentRate([FromQuery] int userId, [FromQuery] DateTime start, [FromQuery] DateTime end, [FromQuery] long? step = null, CancellationToken cancellationToken = default) =>
        Ok(await moneyFlowService.GetInvestmentRate(userId, start, end).ToListAsync(cancellationToken));
}
