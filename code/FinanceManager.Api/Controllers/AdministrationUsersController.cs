using FinanceManager.Application.Providers;
using FinanceManager.Application.Services;
using FinanceManager.Domain.Entities;
using FinanceManager.Domain.Entities.User;
using FinanceManager.Domain.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinanceManager.Api.Controllers;
[Route("api/[controller]")]
[Authorize]
[ApiController]
public class AdministrationUsersController(IUserRepository userRepository, UserPlanVerifier userPlanVerifier, PricingProvider pricingProvider, ILogger<UserController> logger) : ControllerBase
{
    private readonly IUserRepository _userRepository = userRepository;
    private readonly UserPlanVerifier _userPlanVerifier = userPlanVerifier;
    private readonly PricingProvider _pricingProvider = pricingProvider;
    private readonly ILogger<UserController> _logger = logger;


    [HttpGet]
    [Route("GetNewUsersDaily")]
    public async Task<IActionResult> GetNewUsersDaily()
    {
        return BadRequest();

        DateTime start = DateTime.Now.AddDays(32);
        try
        {
            var results = Enumerable.Range(1, 32).Select(x => new ChartEntryModel()
            {
                Date = start.AddDays(x),
                Value = Random.Shared.Next(1, 200)
            });

            return Ok(results);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error getting accounts count");
        }
        return BadRequest();
    }


    [HttpGet]
    [Route("GetDailyActiveUsers")]
    public async Task<IActionResult> GetDailyActiveUsers()
    {
        DateTime start = DateTime.Now.AddDays(32);
        try
        {
            var results = Enumerable.Range(1, 32).Select(x => new ChartEntryModel()
            {
                Date = start.AddDays(x),
                Value = Random.Shared.Next(1, 200)
            });

            return Ok(results);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error getting accounts count");
        }
        return BadRequest();
    }


    [HttpGet]
    [Route("GetAccountsCount")]
    public async Task<IActionResult> GetAccountsCount()
    {
        try
        {
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error getting accounts count");
            return BadRequest();
        }
        return BadRequest();
    }

    [HttpGet]
    [Route("GetTotalTrackedMoney")]
    public async Task<IActionResult> GetTotalTrackedMoney()
    {
        try
        {
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error getting total tracked money");
            return BadRequest();
        }

        return BadRequest();
    }

    [HttpGet]
    [Route("GetUsersCount")]
    public async Task<IActionResult> GetUsersCount()
    {
        try
        {
            return Ok(await _userRepository.GetUsersCount());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error getting user count");
            return BadRequest();
        }
    }

    [HttpGet]
    [Route("GetUsers/{recordIndex:int}/{recordsCount:int}")]
    public async Task<IActionResult> GetUsers(int recordIndex, int recordsCount)
    {
        try
        {
            var users = await _userRepository.GetUsers(recordIndex, recordsCount);
            List<UserDetails> results = users.Select(users => new UserDetails()
            {
                Id = users.UserId,
                Login = users.Login,
                PricingLevel = users.PricingLevel,
                RecordCapacity = new Domain.Entities.Login.RecordCapacity()
                {
                    UsedCapacity = _userPlanVerifier.GetUsedRecordsCapacity(users.UserId).Result,
                    TotalCapacity = _pricingProvider.GetMaxAllowedEntries(users.PricingLevel)
                }
            }).ToList();

            return Ok(results);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error getting user count");
            return BadRequest();
        }

    }
}
