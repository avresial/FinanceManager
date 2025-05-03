using FinanceManager.Application.Commands.User;
using FinanceManager.Application.Providers;
using FinanceManager.Application.Services;
using FinanceManager.Domain.Entities.Login;
using FinanceManager.Domain.Enums;
using FinanceManager.Domain.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace FinanceManager.Api.Controllers;

[Route("api/[controller]")]
[ApiController]
public class UserController(IUserRepository loginRepository, UserPlanVerifier userPlanVerifier, PricingProvider pricingProvider, ILogger<UserController> logger) : ControllerBase
{
    private readonly IUserRepository _loginRepository = loginRepository;
    private readonly UserPlanVerifier _userPlanVerifier = userPlanVerifier;
    private readonly PricingProvider _pricingProvider = pricingProvider;
    private readonly ILogger<UserController> _logger = logger;

    [AllowAnonymous]
    [HttpPost]
    [Route("Add")]
    public async Task<IActionResult> Add(AddUser addUserCommand)
    {
        var existingUser = await _loginRepository.GetUser(addUserCommand.userName);
        if (existingUser is not null) return BadRequest();

        var encryptedPassword = PasswordEncryptionProvider.EncryptPassword(addUserCommand.password);
        var result = await _loginRepository.AddUser(addUserCommand.userName, encryptedPassword, addUserCommand.pricingLevel, UserRole.User);

        if (result) return Ok(result);
        return BadRequest();
    }

    [Authorize]
    [HttpGet]
    [Route("Get/{userId:int}")]
    public async Task<IActionResult> Get(int userId)
    {
        var result = await _loginRepository.GetUser(userId);

        if (result is not null) return Ok(result);
        return BadRequest();
    }

    [Authorize]
    [HttpGet]
    [Route("GetRecordCapacity/{userId:int}")]
    public async Task<IActionResult> GetRecordCapacity(int userId)
    {
        if (!IsValidUserOrAdmin(userId)) return BadRequest();

        var user = await _loginRepository.GetUser(userId);
        if (user is null) return BadRequest();

        try
        {
            var result = new RecordCapacity()
            {
                TotalCapacity = _pricingProvider.GetMaxAllowedEntries(user.PricingLevel),
                UsedCapacity = await _userPlanVerifier.GetUsedRecordsCapacity(userId)
            };
            if (result is not null) return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while getting record capacity for user {UserId}", userId);
            return StatusCode(500, "Unable to retrieve record capacity.");
        }

        return BadRequest();
    }


    [Authorize]
    [HttpDelete]
    [Route("Delete/{userId:int}")]
    public async Task<IActionResult> Delete(int userId)
    {
        if (!IsValidUserOrAdmin(userId)) return BadRequest();

        var result = await _loginRepository.RemoveUser(userId);

        return Ok(result);
    }

    [Authorize]
    [HttpPut]
    [Route("UpdatePassword")]
    public async Task<IActionResult> UpdatePassword(UpdatePassword updatePassword)
    {
        if (!IsValidUserOrAdmin(updatePassword.UserId)) return BadRequest();

        var encryptedPassword = PasswordEncryptionProvider.EncryptPassword(updatePassword.Password);
        var result = await _loginRepository.UpdatePassword(updatePassword.UserId, encryptedPassword);
        if (result) return Ok(result);
        return BadRequest();
    }

    [Authorize]
    [HttpPut]
    [Route("UpdatePricingPlan")]
    public async Task<IActionResult> UpdatePricingPlan(UpdatePricingPlan updatePricingPlan)
    {
        if (!IsValidUserOrAdmin(updatePricingPlan.UserId)) return BadRequest();

        var result = await _loginRepository.UpdatePricingPlan(updatePricingPlan.UserId, updatePricingPlan.PricingLevel);
        if (result) return Ok(result);
        return BadRequest();
    }

    private bool IsValidUserOrAdmin(int userId)
    {
        string? role = User?.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Role)?.Value;

        if (!string.IsNullOrEmpty(role) && role == UserRole.Admin.ToString()) return true;

        var idValue = User?.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;
        if (idValue is null) return false;

        int id = int.Parse(idValue);
        if (id != userId) return false;
        return true;
    }
}
