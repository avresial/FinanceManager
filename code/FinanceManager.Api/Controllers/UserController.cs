using FinanceManager.Api.Helpers;
using FinanceManager.Application.Identity;
using FinanceManager.Application.Identity.Users;
using FinanceManager.Domain.Commands.User;
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
        // Logins are case-insensitive emails; store them lowercased so they match the lowercased lookup performed at
        // login (LoginService lowercases the username). Without this, a login with any uppercase letter is stored
        // verbatim but never found at sign-in on case-sensitive providers like PostgreSQL.
        var login = addUserCommand.UserName.ToLowerInvariant();

        var existingUser = await userRepository.GetUser(login);
        if (existingUser is not null) return Conflict();

        var encryptedPassword = PasswordEncryptionProvider.EncryptPassword(addUserCommand.Password);
        try
        {
            var result = await userRepository.AddUser(login, encryptedPassword, addUserCommand.PricingLevel, UserRole.User, addUserCommand.FirstName, addUserCommand.LastName);
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
        if (!ApiAuthenticationHelper.IsAdminOrAuthenticatedUser(User, userId))
            return Forbid();

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
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Delete(int userId, CancellationToken cancellationToken = default)
    {
        // A "User"-role caller may only delete their own account; Admins may delete anyone.
        if (!ApiAuthenticationHelper.IsAdminOrAuthenticatedUser(User, userId))
            return Forbid();

        return Ok(await usersService.DeleteUser(userId));
    }

    [Authorize(Roles = "Admin, User")]
    [HttpPut]
    [Route("UpdatePassword")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(bool))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdatePassword(UpdatePassword updatePassword, CancellationToken cancellationToken = default)
    {
        // A "User"-role caller may only change their own password and must prove they know the current one.
        // Admins may reset any user's password without supplying the current password.
        if (!ApiAuthenticationHelper.IsAdminOrAuthenticatedUser(User, updatePassword.UserId))
            return Forbid();

        if (!ApiAuthenticationHelper.IsAdmin(User))
        {

            var user = await userRepository.GetUser(updatePassword.UserId);
            if (user is null) return NotFound();

            if (string.IsNullOrEmpty(updatePassword.CurrentPassword))
                return BadRequest("Current password is required.");

            var encryptedCurrentPassword = PasswordEncryptionProvider.EncryptPassword(updatePassword.CurrentPassword);
            if (await userRepository.GetUser(user.Login, encryptedCurrentPassword) is null)
                return BadRequest("Current password is incorrect.");
        }

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