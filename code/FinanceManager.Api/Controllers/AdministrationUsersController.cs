using FinanceManager.Domain.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinanceManager.Api.Controllers;

[Route("api/[controller]")]
[Authorize]
[ApiController]
public class AdministrationUsersController(IAdministrationUsersService administrationUsersService, ILogger<UserController> logger) : ControllerBase
{

    [HttpGet]
    [Route("GetNewUsersDaily")]
    public async Task<IActionResult> GetNewUsersDaily()
    {
        try
        {
            return Ok(await administrationUsersService.GetNewUsersDaily().ToListAsync());
        }
        catch (Exception ex)
        {
            logger.LogError(ex, $"Error getting new users daily");
        }

        return BadRequest("Failed to retrieve new users daily data");
    }


    [HttpGet]
    [Route("GetDailyActiveUsers")]
    public async Task<IActionResult> GetDailyActiveUsers()
    {
        try
        {
            return Ok(await administrationUsersService.GetDailyActiveUsers().ToListAsync());
        }
        catch (Exception ex)
        {
            logger.LogError(ex, $"Error getting daily active users");
        }

        return BadRequest();
    }


    [HttpGet]
    [Route("GetAccountsCount")]
    public async Task<IActionResult> GetAccountsCount()
    {
        try
        {
            var result = await administrationUsersService.GetAccountsCount();
            return Ok(result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, $"Error getting accounts count");
        }

        return BadRequest();
    }

    [HttpGet]
    [Route("GetTotalTrackedMoney")]
    public async Task<IActionResult> GetTotalTrackedMoney()
    {
        try
        {
            var result = await administrationUsersService.GetTotalTrackedMoney();
            if (result is null)
                return NoContent();
            else
                return Ok(result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, $"Error getting total tracked money");
        }

        return BadRequest();
    }

    [HttpGet]
    [Route("GetUsersCount")]
    public async Task<IActionResult> GetUsersCount()
    {
        try
        {
            return Ok(await administrationUsersService.GetUsersCount());
        }
        catch (Exception ex)
        {
            logger.LogError(ex, $"Error getting user count");
        }

        return BadRequest();
    }

    [HttpGet]
    [Route("GetUsers/{recordIndex:int}/{recordsCount:int}")]
    public async Task<IActionResult> GetUsers(int recordIndex, int recordsCount)
    {
        if (recordIndex < 0 || recordsCount <= 0) return BadRequest("Invalid pagination parameters");

        try
        {
            return Ok(await administrationUsersService.GetUsers(recordIndex, recordsCount).ToListAsync());
        }
        catch (Exception ex)
        {
            logger.LogError(ex, $"Error getting users with pagination");
        }

        return BadRequest();
    }
}
