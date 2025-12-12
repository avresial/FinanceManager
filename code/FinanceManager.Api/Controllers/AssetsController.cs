using FinanceManager.Domain.Entities.MoneyFlowModels;
using FinanceManager.Domain.Enums;
using FinanceManager.Domain.Repositories;
using FinanceManager.Domain.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinanceManager.Api.Controllers;

[Route("api/[controller]")]
[Authorize]
[ApiController]
[Tags("Financial Analysis")]
public class AssetsController(IAssetsService assetsService, ICurrencyRepository currencyRepository) : ControllerBase
{
    [HttpGet("IsAnyAccountWithAssets/{userId:int}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(bool))]
    public async Task<IActionResult> IsAnyAccountWithAssets(int userId, CancellationToken cancellationToken = default) =>
           Ok(await assetsService.IsAnyAccountWithAssets(userId));

    [HttpGet("GetEndAssetsPerAccount/{userId:int}/{currencyId:int}/{asOfDate:DateTime}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(List<NameValueResult>))]
    public async Task<IActionResult> GetEndAssetsPerAccount(int userId, int currencyId, DateTime asOfDate, CancellationToken cancellationToken = default) =>
        Ok(await assetsService.GetEndAssetsPerAccount(userId, await currencyRepository.GetCurrencies(cancellationToken).SingleAsync(x => x.Id == currencyId, cancellationToken), asOfDate).ToListAsync(cancellationToken));

    [HttpGet("GetEndAssetsPerType/{userId:int}/{currencyId:int}/{asOfDate:DateTime}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(List<NameValueResult>))]
    public async Task<IActionResult> GetEndAssetsPerType(int userId, int currencyId, DateTime asOfDate, CancellationToken cancellationToken = default) =>
        Ok(await assetsService.GetEndAssetsPerType(userId, await currencyRepository.GetCurrencies(cancellationToken).SingleAsync(x => x.Id == currencyId, cancellationToken), asOfDate).ToListAsync(cancellationToken));

    [HttpGet("GetAssetsTimeSeries/{userId:int}/{currencyId:int}/{start:DateTime}/{end:DateTime}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(List<TimeSeriesModel>))]
    public async Task<IActionResult> GetAssetsTimeSeries(int userId, int currencyId, DateTime start, DateTime end, CancellationToken cancellationToken = default) =>
        Ok(await assetsService.GetAssetsTimeSeries(userId, await currencyRepository.GetCurrencies(cancellationToken).SingleAsync(x => x.Id == currencyId, cancellationToken), start, end));

    [HttpGet("GetAssetsTimeSeries/{userId:int}/{currencyId:int}/{start:DateTime}/{end:DateTime}/{investmentType}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(List<TimeSeriesModel>))]
    public async Task<IActionResult> GetAssetsTimeSeries(int userId, int currencyId, DateTime start, DateTime end, InvestmentType investmentType, CancellationToken cancellationToken = default) =>
        Ok(await assetsService.GetAssetsTimeSeries(userId, await currencyRepository.GetCurrencies(cancellationToken).SingleAsync(x => x.Id == currencyId, cancellationToken), start, end, investmentType));

}