using FinanceManager.Domain.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinanceManager.Api.Controllers;
[Route("api/[controller]")]
[Authorize]
[ApiController]
public class AdministrationUsersController(IAdministrationUsersService administrationUsersService, ILogger<UserController> logger) : ControllerBase
{
    private readonly IAdministrationUsersService _administrationUsersService = administrationUsersService;
    private readonly ILogger<UserController> _logger = logger;


    [HttpGet]
    [Route("GetNewUsersDaily")]
    public async Task<IActionResult> GetNewUsersDaily()
    {
        try
        {
            return Ok(await _administrationUsersService.GetNewUsersDaily());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error getting new users daily");
        }

        return BadRequest("Failed to retrieve new users daily data");
    }


    [HttpGet]
    [Route("GetDailyActiveUsers")]
    public async Task<IActionResult> GetDailyActiveUsers()
    {
        try
        {
            return Ok(await _administrationUsersService.GetDailyActiveUsers());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error getting daily active users");
        }

        return BadRequest();
    }


    [HttpGet]
    [Route("GetAccountsCount")]
    public async Task<IActionResult> GetAccountsCount()
    {
        try
        {
            var result = await _administrationUsersService.GetAccountsCount();
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error getting accounts count");
        }

        return BadRequest();
    }

    [HttpGet]
    [Route("GetTotalTrackedMoney")]
    public async Task<IActionResult> GetTotalTrackedMoney()
    {
        try
        {
            var result = await _administrationUsersService.GetTotalTrackedMoney();
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error getting total tracked money");
        }

        return BadRequest();
    }

    [HttpGet]
    [Route("GetUsersCount")]
    public async Task<IActionResult> GetUsersCount()
    {
        try
        {
            return Ok(await _administrationUsersService.GetUsersCount());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error getting user count");
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
            return Ok(await _administrationUsersService.GetUsers(recordIndex, recordsCount));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error getting users with pagination");
        }

        return BadRequest();
    }
}
