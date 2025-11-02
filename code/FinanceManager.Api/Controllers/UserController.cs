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
public class UserController(IUserRepository userRepository, IUserPlanVerifier userPlanVerifier, ILogger<UserController> logger) : ControllerBase
{

    [AllowAnonymous]
    [HttpPost]
    [Route("Add")]
    public async Task<IActionResult> Add(AddUser addUserCommand)
    {
        var existingUser = await userRepository.GetUser(addUserCommand.userName);
        if (existingUser is not null) return BadRequest();

        var encryptedPassword = PasswordEncryptionProvider.EncryptPassword(addUserCommand.password);
        var result = await userRepository.AddUser(addUserCommand.userName, encryptedPassword, addUserCommand.pricingLevel, UserRole.User);

        if (result) return Ok(result);
        return BadRequest();
    }

    [Authorize]
    [HttpGet]
    [Route("Get/{userId:int}")]
    public async Task<IActionResult> Get(int userId)
    {
        var result = await userRepository.GetUser(userId);

        if (result is not null) return Ok(result);
        return BadRequest();
    }

    [Authorize]
    [HttpGet]
    [Route("GetRecordCapacity/{userId:int}")]
    public async Task<IActionResult> GetRecordCapacity(int userId)
    {
        if (!IsValidUserOrAdmin(userId)) return BadRequest();

        var user = await userRepository.GetUser(userId);
        if (user is null) return BadRequest();

        try
        {
            var result = new RecordCapacity()
            {
                TotalCapacity = PricingProvider.GetMaxAllowedEntries(user.PricingLevel),
                UsedCapacity = await userPlanVerifier.GetUsedRecordsCapacity(userId)
            };
            if (result is not null) return Ok(result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error while getting record capacity for user {UserId}", userId);
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

        var result = await userRepository.RemoveUser(userId);

        return Ok(result);
    }

    [Authorize]
    [HttpPut]
    [Route("UpdatePassword")]
    public async Task<IActionResult> UpdatePassword(UpdatePassword updatePassword)
    {
        if (!IsValidUserOrAdmin(updatePassword.UserId)) return BadRequest();

        var encryptedPassword = PasswordEncryptionProvider.EncryptPassword(updatePassword.Password);
        var result = await userRepository.UpdatePassword(updatePassword.UserId, encryptedPassword);
        if (result) return Ok(result);
        return BadRequest();
    }

    [Authorize]
    [HttpPut]
    [Route("UpdatePricingPlan")]
    public async Task<IActionResult> UpdatePricingPlan(UpdatePricingPlan updatePricingPlan)
    {
        if (!IsValidUserOrAdmin(updatePricingPlan.UserId)) return BadRequest();

        var result = await userRepository.UpdatePricingPlan(updatePricingPlan.UserId, updatePricingPlan.PricingLevel);
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
