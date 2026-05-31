using FinanceManager.Application.Commands.User;
using FinanceManager.Application.Providers;
using FinanceManager.Application.Services;
using FinanceManager.Domain.Entities.Users;
using FinanceManager.Domain.Enums;
using FinanceManager.Domain.Exceptions;
using FinanceManager.Domain.Repositories;
using FinanceManager.Infrastructure.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinanceManager.Api.Controllers;

[Route("api/[controller]")]
[ApiController]
[Tags("Users")]
public class UserController(IUserRepository userRepository, UsersService usersService, IUserPlanVerifier userPlanVerifier) : ControllerBase
{

    [AllowAnonymous]
    [HttpPost]
    [Route("Add")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(bool))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Add(AddUser addUserCommand, CancellationToken cancellationToken = default)
    {
        var existingUser = await userRepository.GetUser(addUserCommand.UserName);
        if (existingUser is not null) return Conflict();

        var encryptedPassword = PasswordEncryptionProvider.EncryptPassword(addUserCommand.Password);
        try
        {
            var result = await userRepository.AddUser(addUserCommand.UserName, encryptedPassword, addUserCommand.PricingLevel, UserRole.User, addUserCommand.FirstName, addUserCommand.LastName);
            return result ? Ok(result) : BadRequest();
        }
        catch (DuplicateLoginException)
        {
            // A concurrent request created the same login between the lookup above and this insert. Surface it as a
            // conflict, not a 500.
            return Conflict();
        }
    }

    [Authorize]
    [HttpGet]
    [Route("Get/{userId:int}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(UserDto))]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(int userId, CancellationToken cancellationToken = default)
    {
        var result = await userRepository.GetUser(userId);
        return result is not null ? Ok(result) : NotFound();
    }


    [Authorize(Roles = "Admin")]
    [HttpGet]
    [Route("GetRecordCapacity/{userId:int}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(RecordCapacity))]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetRecordCapacity(int userId, CancellationToken cancellationToken = default)
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
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(bool))]
    public async Task<IActionResult> Delete(int userId, CancellationToken cancellationToken = default) => Ok(await usersService.DeleteUser(userId));

    [Authorize(Roles = "Admin, User")]
    [HttpPut]
    [Route("UpdatePassword")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(bool))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdatePassword(UpdatePassword updatePassword, CancellationToken cancellationToken = default)
    {
        var encryptedPassword = PasswordEncryptionProvider.EncryptPassword(updatePassword.Password);
        var result = await userRepository.UpdatePassword(updatePassword.UserId, encryptedPassword);
        return result ? Ok(result) : BadRequest();
    }

    [Authorize(Roles = "Admin")]
    [HttpPut]
    [Route("UpdatePricingPlan")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(bool))]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdatePricingPlan(UpdatePricingPlan updatePricingPlan, CancellationToken cancellationToken = default)
    {
        var result = await userRepository.UpdatePricingPlan(updatePricingPlan.UserId, updatePricingPlan.PricingLevel);
        return result ? Ok(result) : NotFound();
    }
}