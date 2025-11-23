using FinanceManager.Application.Commands.User;
using FinanceManager.Application.Providers;
using FinanceManager.Application.Services;
using FinanceManager.Domain.Entities.Users;
using FinanceManager.Domain.Enums;
using FinanceManager.Domain.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinanceManager.Api.Controllers;

[Route("api/[controller]")]
[ApiController]
public class UserController(IUserRepository userRepository, UsersService usersService, IUserPlanVerifier userPlanVerifier) : ControllerBase
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

        return result ? Ok(result) : BadRequest();
    }

    [Authorize]
    [HttpGet]
    [Route("Get/{userId:int}")]
    public async Task<IActionResult> Get(int userId)
    {
        var result = await userRepository.GetUser(userId);
        return result is not null ? Ok(result) : NotFound();
    }


    [Authorize(Roles = "Admin")]
    [HttpGet]
    [Route("GetRecordCapacity/{userId:int}")]
    public async Task<IActionResult> GetRecordCapacity(int userId)
    {
        var user = await userRepository.GetUser(userId);
        if (user is null) return NotFound();

        return Ok(new RecordCapacity()
        {
            TotalCapacity = PricingProvider.GetMaxAllowedEntries(user.PricingLevel),
            UsedCapacity = await userPlanVerifier.GetUsedRecordsCapacity(userId)
        });
    }


    [Authorize(Roles = "Admin, User")]
    [HttpDelete]
    [Route("Delete/{userId:int}")]
    public async Task<IActionResult> Delete(int userId) => Ok(await usersService.DeleteUser(userId));

    [Authorize(Roles = "Admin, User")]
    [HttpPut]
    [Route("UpdatePassword")]
    public async Task<IActionResult> UpdatePassword(UpdatePassword updatePassword)
    {
        var encryptedPassword = PasswordEncryptionProvider.EncryptPassword(updatePassword.Password);
        var result = await userRepository.UpdatePassword(updatePassword.UserId, encryptedPassword);
        return result ? Ok(result) : BadRequest();
    }

    [Authorize(Roles = "Admin")]
    [HttpPut]
    [Route("UpdatePricingPlan")]
    public async Task<IActionResult> UpdatePricingPlan(UpdatePricingPlan updatePricingPlan)
    {
        var result = await userRepository.UpdatePricingPlan(updatePricingPlan.UserId, updatePricingPlan.PricingLevel);
        return result ? Ok(result) : NotFound();
    }
}