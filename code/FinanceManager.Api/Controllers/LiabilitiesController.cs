using FinanceManager.Domain.Entities.MoneyFlowModels;
using FinanceManager.Domain.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinanceManager.Api.Controllers;

[Authorize]
[Route("api/[controller]")]
[ApiController]
[Tags("Financial Analysis")]
public class LiabilitiesController(ILiabilitiesService liabilitiesService) : ControllerBase
{
    [HttpGet("IsAnyAccountWithLiabilities/{userId:int}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(bool))]
    public async Task<IActionResult> IsAnyAccountWithLiabilities(int userId, CancellationToken cancellationToken = default) =>
        Ok(await liabilitiesService.IsAnyAccountWithLiabilities(userId));

    [HttpGet("GetEndLiabilitiesPerAccount/{userId:int}/{start:DateTime}/{end:DateTime}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(List<NameValueResult>))]
    public async Task<IActionResult> GetEndLiabilitiesPerAccount(int userId, DateTime start, DateTime end, CancellationToken cancellationToken = default) =>
        Ok(await liabilitiesService.GetEndLiabilitiesPerAccount(userId, start, end).ToListAsync(cancellationToken));

    [HttpGet("GetEndLiabilitiesPerType/{userId:int}/{start:DateTime}/{end:DateTime}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(List<NameValueResult>))]
    public async Task<IActionResult> GetEndLiabilitiesPerType(int userId, DateTime start, DateTime end, CancellationToken cancellationToken = default) =>
        Ok(await liabilitiesService.GetEndLiabilitiesPerType(userId, start, end).ToListAsync(cancellationToken));

    [HttpGet("GetLiabilitiesTimeSeries/{userId:int}/{start:DateTime}/{end:DateTime}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(List<TimeSeriesModel>))]
    public async Task<IActionResult> GetLiabilitiesTimeSeries(int userId, DateTime start, DateTime end, CancellationToken cancellationToken = default) =>
        Ok(await liabilitiesService.GetLiabilitiesTimeSeries(userId, start, end).ToListAsync(cancellationToken));

}