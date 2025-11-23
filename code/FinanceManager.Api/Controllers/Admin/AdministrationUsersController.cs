using FinanceManager.Domain.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinanceManager.Api.Controllers;

[Route("api/[controller]")]
[Authorize]
[ApiController]
public class AdministrationUsersController(IAdministrationUsersService administrationUsersService) : ControllerBase
{

    [HttpGet(nameof(GetNewUsersDaily))]
    public async Task<IActionResult> GetNewUsersDaily() =>
        Ok(await administrationUsersService.GetNewUsersDaily().ToListAsync());


    [HttpGet(nameof(GetDailyActiveUsers))]
    public async Task<IActionResult> GetDailyActiveUsers() =>
        Ok(await administrationUsersService.GetDailyActiveUsers().ToListAsync());

    [HttpGet(nameof(GetAccountsCount))]
    public async Task<IActionResult> GetAccountsCount() =>
        Ok(await administrationUsersService.GetAccountsCount());

    [HttpGet(nameof(GetTotalTrackedMoney))]
    public async Task<IActionResult> GetTotalTrackedMoney() =>
        Ok(await administrationUsersService.GetTotalTrackedMoney());

    [HttpGet(nameof(GetUsersCount))]
    public async Task<IActionResult> GetUsersCount() =>
        Ok(await administrationUsersService.GetUsersCount());

    [HttpGet("GetUsers/{recordIndex:int}/{recordsCount:int}")]
    public async Task<IActionResult> GetUsers(int recordIndex, int recordsCount)
    {
        if (recordIndex < 0 || recordsCount <= 0) return BadRequest("Invalid pagination parameters");

        return Ok(await administrationUsersService.GetUsers(recordIndex, recordsCount).ToListAsync());
    }
}