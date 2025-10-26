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
    public async Task<IActionResult> GetEndAssetsPerAccount(int userId, string currency, DateTime start, DateTime end) =>
        Ok(await assetsService.GetEndAssetsPerAccount(userId, new(currency, string.Empty), start, end));

    [HttpGet("GetEndAssetsPerType/{userId:int}/{currency}/{start:DateTime}/{end:DateTime}")]
    public async Task<IActionResult> GetEndAssetsPerType(int userId, string currency, DateTime start, DateTime end) =>
        Ok(await assetsService.GetEndAssetsPerType(userId, new(currency, string.Empty), start, end));

    [HttpGet("GetAssetsTimeSeries/{userId:int}/{currency}/{start:DateTime}/{end:DateTime}")]
    public async Task<IActionResult> GetAssetsTimeSeries(int userId, string currency, DateTime start, DateTime end) =>
        Ok(await assetsService.GetAssetsTimeSeries(userId, new(currency, string.Empty), start, end));

    [HttpGet("GetAssetsTimeSeries/{userId:int}/{currency}/{start:DateTime}/{end:DateTime}/{investmentType}")]
    public async Task<IActionResult> GetAssetsTimeSeries(int userId, string currency, DateTime start, DateTime end, InvestmentType investmentType) =>
        Ok(await assetsService.GetAssetsTimeSeries(userId, new(currency, string.Empty), start, end, investmentType));

}