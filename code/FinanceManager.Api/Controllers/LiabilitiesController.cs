using FinanceManager.Domain.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinanceManager.Api.Controllers;

[Authorize]
[Route("api/[controller]")]
[ApiController]
public class LiabilitiesController(ILiabilitiesService liabilitiesService) : ControllerBase
{
    [HttpGet("IsAnyAccountWithLiabilities/{userId:int}")]
    public async Task<IActionResult> IsAnyAccountWithLiabilities(int userId) =>
        Ok(await liabilitiesService.IsAnyAccountWithLiabilities(userId));

    [HttpGet("GetEndLiabilitiesPerAccount/{userId:int}/{start:DateTime}/{end:DateTime}")]
    public async Task<IActionResult> GetEndLiabilitiesPerAccount(int userId, DateTime start, DateTime end) =>
        Ok(await liabilitiesService.GetEndLiabilitiesPerAccount(userId, start, end).ToListAsync());

    [HttpGet("GetEndLiabilitiesPerType/{userId:int}/{start:DateTime}/{end:DateTime}")]
    public async Task<IActionResult> GetEndLiabilitiesPerType(int userId, DateTime start, DateTime end) =>
        Ok(await liabilitiesService.GetEndLiabilitiesPerType(userId, start, end).ToListAsync());

    [HttpGet("GetLiabilitiesTimeSeries/{userId:int}/{start:DateTime}/{end:DateTime}")]
    public async Task<IActionResult> GetLiabilitiesTimeSeries(int userId, DateTime start, DateTime end) =>
        Ok(await liabilitiesService.GetLiabilitiesTimeSeries(userId, start, end).ToListAsync());

}
