using FinanceManager.Api.Services;
using FinanceManager.Application.Commands.Login;
using FinanceManager.Application.Services;
using FinanceManager.Domain.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinanceManager.Api.Controllers;

[Route("api/[controller]")]
[ApiController]
public class LoginController(JwtTokenGenerator jwtTokenGenerator, IUserRepository userRepository, IActiveUsersRepository activeUsersRepository,
    GuestAccountSeeder guestAccountSeeder, ILogger<LoginController> logger) : ControllerBase
{

    [AllowAnonymous]
    [HttpPost(Name = "Login")]
    public async Task<IActionResult> Login(LoginRequestModel requestModel)
    {
        try
        {
            if (requestModel.userName == "guest")
                await guestAccountSeeder.SeedNewData(DateTime.UtcNow.AddMonths(-3), DateTime.UtcNow);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error occurred while seeding guest account data");
        }

        var user = await userRepository.GetUser(requestModel.userName, requestModel.password);

        if (user is null) return BadRequest();

        var token = jwtTokenGenerator.GenerateToken(requestModel.userName, user.UserId, user.UserRole);

        try
        {
            if (token is not null) await activeUsersRepository.Add(token.UserId, DateOnly.FromDateTime(DateTime.UtcNow));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error occurred while adding user to active users repository");
        }

        return Ok(token);
    }
}