using FinanceManager.Domain.Entities.MoneyFlowModels;
using FinanceManager.Domain.Repositories;
using FinanceManager.Domain.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinanceManager.Api.Controllers;

[Authorize]
[Route("api/[controller]")]
[ApiController]
[Tags("Financial Analysis")]
public class MoneyFlowController(IMoneyFlowService moneyFlowService, IBalanceService balanceService, ICurrencyRepository currencyRepository) : ControllerBase
{
    [HttpGet("GetNetWorth/{userId:int}/{currencyId:int}/{date:DateTime}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(decimal))]
    public async Task<IActionResult> GetNetWorth(int userId, int currencyId, DateTime date, CancellationToken cancellationToken = default) =>
        Ok(await moneyFlowService.GetNetWorth(userId, await currencyRepository.GetCurrencies(cancellationToken).SingleAsync(x => x.Id == currencyId, cancellationToken), date));

    [HttpGet("GetNetWorth/{userId:int}/{currencyId:int}/{start:DateTime}/{end:DateTime}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(Dictionary<DateTime, decimal>))]
    public async Task<IActionResult> GetNetWorth(int userId, int currencyId, DateTime start, DateTime end, CancellationToken cancellationToken = default) =>
        Ok(await moneyFlowService.GetNetWorth(userId, await currencyRepository.GetCurrencies(cancellationToken).SingleAsync(x => x.Id == currencyId, cancellationToken), start, end));

    [HttpGet("GetInflow/{userId:int}/{currencyId:int}/{start:DateTime}/{end:DateTime}/{step:long?}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(List<TimeSeriesModel>))]
    public async Task<IActionResult> GetInflow(int userId, int currencyId, DateTime start, DateTime end, long? step = null, [FromQuery] List<int>? accountIds = null, CancellationToken cancellationToken = default)
    {
        var currency = await currencyRepository.GetCurrencies(cancellationToken).SingleAsync(x => x.Id == currencyId, cancellationToken);
        return Ok(accountIds is { Count: > 0 }
            ? await balanceService.GetInflow(userId, currency, start, end, accountIds)
            : await balanceService.GetInflow(userId, currency, start, end));
    }

    [HttpGet("GetOutflow/{userId:int}/{currencyId:int}/{start:DateTime}/{end:DateTime}/{step:long?}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(List<TimeSeriesModel>))]
    public async Task<IActionResult> GetOutflow(int userId, int currencyId, DateTime start, DateTime end, long? step = null, [FromQuery] List<int>? accountIds = null, CancellationToken cancellationToken = default)
    {
        var currency = await currencyRepository.GetCurrencies(cancellationToken).SingleAsync(x => x.Id == currencyId, cancellationToken);
        return Ok(accountIds is { Count: > 0 }
            ? await balanceService.GetOutflow(userId, currency, start, end, accountIds)
            : await balanceService.GetOutflow(userId, currency, start, end));
    }

    [HttpGet("GetNetCashFlow/{userId:int}/{currencyId:int}/{start:DateTime}/{end:DateTime}/{step:long?}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(List<TimeSeriesModel>))]
    public async Task<IActionResult> GetNetCashFlow(int userId, int currencyId, DateTime start, DateTime end, long? step = null, [FromQuery] List<int>? accountIds = null, CancellationToken cancellationToken = default)
    {
        var currency = await currencyRepository.GetCurrencies(cancellationToken).SingleAsync(x => x.Id == currencyId, cancellationToken);
        return Ok(accountIds is { Count: > 0 }
            ? await balanceService.GetNetCashFlow(userId, currency, start, end, accountIds)
            : await balanceService.GetNetCashFlow(userId, currency, start, end));
    }

    [HttpGet("GetClosingBalance/{userId:int}/{currencyId:int}/{start:DateTime}/{end:DateTime}/{step:long?}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(List<TimeSeriesModel>))]
    public async Task<IActionResult> GetClosingBalance(int userId, int currencyId, DateTime start, DateTime end, long? step = null, [FromQuery] List<int>? accountIds = null, CancellationToken cancellationToken = default)
    {
        var currency = await currencyRepository.GetCurrencies(cancellationToken).SingleAsync(x => x.Id == currencyId, cancellationToken);
        return Ok(accountIds is { Count: > 0 }
            ? await balanceService.GetClosingBalance(userId, currency, start, end, accountIds)
            : await balanceService.GetClosingBalance(userId, currency, start, end));
    }

    [HttpGet("GetLabelsValue")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(NameValueResult))]
    public async Task<IActionResult> GetLabelsValue([FromQuery] int userId, [FromQuery] DateTime start, [FromQuery] DateTime end, [FromQuery] long? step = null, CancellationToken cancellationToken = default) =>
        Ok(await moneyFlowService.GetLabelsValue(userId, start, end));

    [HttpGet("GetInvestmentRate")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(List<InvestmentRate>))]
    public async Task<IActionResult> GetInvestmentRate([FromQuery] int userId, [FromQuery] DateTime start, [FromQuery] DateTime end, [FromQuery] long? step = null, CancellationToken cancellationToken = default) =>
        Ok(await moneyFlowService.GetInvestmentRate(userId, start, end).ToListAsync(cancellationToken));
}