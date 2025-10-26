using FinanceManager.Domain.Entities;
using FinanceManager.Domain.Enums;
using FinanceManager.Domain.Services;
using Microsoft.AspNetCore.Mvc;

namespace FinanceManager.Api.Controllers;

[Route("api/[controller]")]
[ApiController]
public class AssetsController(IAssetsService assetsService) : ControllerBase
{
    [HttpGet("IsAnyAccountWithAssets/{userId:int}")]
    public async Task<IActionResult> IsAnyAccountWithAssets(int userId) =>
           Ok(await assetsService.IsAnyAccountWithAssets(userId));

    [HttpGet("GetEndAssetsPerAccount/{userId:int}/{currency}/{start:DateTime}/{end:DateTime}")]
    public async Task<IActionResult> GetEndAssetsPerAccount(int userId, Currency currency, DateTime start, DateTime end) =>
        Ok(await assetsService.GetEndAssetsPerAccount(userId, currency, start, end));

    [HttpGet("GetEndAssetsPerType/{userId:int}/{currency}/{start:DateTime}/{end:DateTime}")]
    public async Task<IActionResult> GetEndAssetsPerType(int userId, Currency currency, DateTime start, DateTime end) =>
        Ok(await assetsService.GetEndAssetsPerType(userId, currency, start, end));

    [HttpGet("GetAssetsTimeSeries/{userId:int}/{currency}/{start:DateTime}/{end:DateTime}")]
    public async Task<IActionResult> GetAssetsTimeSeries(int userId, Currency currency, DateTime start, DateTime end) =>
        Ok(await assetsService.GetAssetsTimeSeries(userId, currency, start, end));

    [HttpGet("GetAssetsTimeSeries/{userId:int}/{currency}/{start:DateTime}/{end:DateTime}/{investmentType}")]
    public async Task<IActionResult> GetAssetsTimeSeries(int userId, Currency currency, DateTime start, DateTime end, InvestmentType investmentType) =>
        Ok(await assetsService.GetAssetsTimeSeries(userId, currency, start, end, investmentType));

}