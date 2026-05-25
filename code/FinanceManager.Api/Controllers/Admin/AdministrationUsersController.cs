using FinanceManager.Domain.Entities.Shared;
using FinanceManager.Domain.Services;
using FinanceManager.Infrastructure.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinanceManager.Api.Controllers;

[Route("api/[controller]")]
[Authorize]
[ApiController]
[Tags("Administration")]
public class AdministrationUsersController(IAdministrationUsersService administrationUsersService) : ControllerBase
{

    [HttpGet(nameof(GetNewUsersDaily))]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(List<ChartEntryModel>))]
    public async Task<IActionResult> GetNewUsersDaily() =>
        Ok(await administrationUsersService.GetNewUsersDaily().ToListAsync());


    [HttpGet(nameof(GetDailyActiveUsers))]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(List<ChartEntryModel>))]
    public async Task<IActionResult> GetDailyActiveUsers() =>
        Ok(await administrationUsersService.GetDailyActiveUsers().ToListAsync());

    [HttpGet(nameof(GetAccountsCount))]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(int))]
    public async Task<IActionResult> GetAccountsCount() =>
        Ok(await administrationUsersService.GetAccountsCount());

    [HttpGet(nameof(GetTotalTrackedMoney))]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(decimal))]
    public async Task<IActionResult> GetTotalTrackedMoney() =>
        Ok(await administrationUsersService.GetTotalTrackedMoney());

    [HttpGet(nameof(GetUsersCount))]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(int))]
    public async Task<IActionResult> GetUsersCount() =>
        Ok(await administrationUsersService.GetUsersCount());

    [HttpGet("GetUsers/{recordIndex:int}/{recordsCount:int}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(List<UserDto>))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetUsers(int recordIndex, int recordsCount)
    {
        if (recordIndex < 0 || recordsCount <= 0) return BadRequest("Invalid pagination parameters");
        var result = administrationUsersService.GetUsers(recordIndex, recordsCount);
        return Ok(await result.ToListAsync());
    }
}