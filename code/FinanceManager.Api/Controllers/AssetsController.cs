using FinanceManager.Domain.Enums;
using FinanceManager.Domain.Repositories;
using FinanceManager.Domain.Services;
using Microsoft.AspNetCore.Mvc;

namespace FinanceManager.Api.Controllers;

[Route("api/[controller]")]
[ApiController]
public class AssetsController(IAssetsService assetsService, ICurrencyRepository currencyRepository) : ControllerBase
{
    [HttpGet("IsAnyAccountWithAssets/{userId:int}")]
    public async Task<IActionResult> IsAnyAccountWithAssets(int userId) =>
           Ok(await assetsService.IsAnyAccountWithAssets(userId));

    [HttpGet("GetEndAssetsPerAccount/{userId:int}/{currencyId:int}/{asOfDate:DateTime}")]
    public async Task<IActionResult> GetEndAssetsPerAccount(int userId, int currencyId, DateTime asOfDate) =>
        Ok(await assetsService.GetEndAssetsPerAccount(userId, await currencyRepository.GetCurrencies().SingleAsync(x => x.Id == currencyId), asOfDate).ToListAsync());

    [HttpGet("GetEndAssetsPerType/{userId:int}/{currencyId:int}/{asOfDate:DateTime}")]
    public async Task<IActionResult> GetEndAssetsPerType(int userId, int currencyId, DateTime asOfDate) =>
        Ok(await assetsService.GetEndAssetsPerType(userId, await currencyRepository.GetCurrencies().SingleAsync(x => x.Id == currencyId), asOfDate).ToListAsync());

    [HttpGet("GetAssetsTimeSeries/{userId:int}/{currencyId:int}/{start:DateTime}/{end:DateTime}")]
    public async Task<IActionResult> GetAssetsTimeSeries(int userId, int currencyId, DateTime start, DateTime end) =>
        Ok(await assetsService.GetAssetsTimeSeries(userId, await currencyRepository.GetCurrencies().SingleAsync(x => x.Id == currencyId), start, end));

    [HttpGet("GetAssetsTimeSeries/{userId:int}/{currencyId:int}/{start:DateTime}/{end:DateTime}/{investmentType}")]
    public async Task<IActionResult> GetAssetsTimeSeries(int userId, int currencyId, DateTime start, DateTime end, InvestmentType investmentType) =>
        Ok(await assetsService.GetAssetsTimeSeries(userId, await currencyRepository.GetCurrencies().SingleAsync(x => x.Id == currencyId), start, end, investmentType));

}