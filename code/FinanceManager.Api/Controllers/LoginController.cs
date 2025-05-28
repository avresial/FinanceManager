using FinanceManager.Api.Services;
using FinanceManager.Application.Commands.Login;
using FinanceManager.Application.Services;
using FinanceManager.Domain.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinanceManager.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class LoginController : ControllerBase
    {
        private readonly JwtTokenGenerator _jwtTokenGenerator;
        private readonly IUserRepository _userRepository;
        private readonly IActiveUsersRepository _activeUsersRepository;
        private readonly GuestAccountSeeder _guestAccountSeeder;
        private readonly ILogger<LoginController> _logger;

        public LoginController(JwtTokenGenerator jwtTokenGenerator, IUserRepository userRepository, IActiveUsersRepository activeUsersRepository, GuestAccountSeeder guestAccountSeeder, ILogger<LoginController> logger)
        {
            _jwtTokenGenerator = jwtTokenGenerator;
            _userRepository = userRepository;
            _activeUsersRepository = activeUsersRepository;
            _guestAccountSeeder = guestAccountSeeder;
            _logger = logger;
        }

        [AllowAnonymous]
        [HttpPost(Name = "Login")]
        public async Task<IActionResult> Login(LoginRequestModel requestModel)
        {
            try
            {
                if (requestModel.userName == "guest")
                    await _guestAccountSeeder.SeedNewData(DateTime.UtcNow.AddMonths(-1), DateTime.UtcNow);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while seeding guest account data");
            }

            var user = await _userRepository.GetUser(requestModel.userName, requestModel.password);

            if (user is null) return BadRequest();

            LoginResponseModel? token = _jwtTokenGenerator.GenerateToken(requestModel.userName, user.UserId, user.UserRole);

            try
            {
                if (token is not null) await _activeUsersRepository.Add(token.UserId, DateOnly.FromDateTime(DateTime.UtcNow));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while adding user to active users repository");
            }

            return Ok(token);
        }
    }
}
