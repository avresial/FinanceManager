using FinanceManager.Domain.Enums;
using FinanceManager.Domain.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinanceManager.Api.Controllers;

[Authorize]
[Route("api/[controller]")]
[ApiController]
public class LiabilitiesController(ILiabilitiesService liabilitiesService) : ControllerBase
{
    private readonly ILiabilitiesService _liabilitiesService = liabilitiesService;

    [HttpGet("IsAnyAccountWithLiabilities/{userId:int}")]
    public async Task<IActionResult> IsAnyAccountWithLiabilities(int userId)
    {
        return Ok(await _liabilitiesService.IsAnyAccountWithLiabilities(userId));
    }

    [HttpGet("GetEndLiabilitiesPerAccount/{userId:int}/{start:DateTime}/{end:DateTime}")]
    public async Task<IActionResult> GetEndLiabilitiesPerAccount(int userId, DateTime start, DateTime end)
    {
        return Ok(await _liabilitiesService.GetEndLiabilitiesPerAccount(userId, start, end));
    }

    [HttpGet("GetEndLiabilitiesPerType/{userId:int}/{start:DateTime}/{end:DateTime}")]
    public async Task<IActionResult> GetEndAssetsPerType(int userId, DateTime start, DateTime end)
    {
        return Ok(await _liabilitiesService.GetEndLiabilitiesPerType(userId, start, end));
    }

    [HttpGet("GetLiabilitiesTimeSeries/{userId:int}/{start:DateTime}/{end:DateTime}")]
    public async Task<IActionResult> GetLiabilitiesTimeSeries(int userId, DateTime start, DateTime end)
    {
        return Ok(await _liabilitiesService.GetLiabilitiesTimeSeries(userId, start, end));
    }

    [HttpGet("GetLiabilitiesTimeSeries/{userId:int}/{start:DateTime}/{end:DateTime}/{investmentType}")]
    public async Task<IActionResult> GetLiabilitiesTimeSeries(int userId, DateTime start, DateTime end, InvestmentType investmentType)
    {
        return Ok(await _liabilitiesService.GetLiabilitiesTimeSeries(userId, start, end, investmentType));
    }
}
