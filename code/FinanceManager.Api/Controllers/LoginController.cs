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
        private readonly GuestAccountSeeder _guestAccountSeeder;

        public LoginController(JwtTokenGenerator jwtTokenGenerator, IUserRepository userRepository, GuestAccountSeeder guestAccountSeeder)
        {
            _jwtTokenGenerator = jwtTokenGenerator;
            _userRepository = userRepository;
            _guestAccountSeeder = guestAccountSeeder;
        }

        [AllowAnonymous]
        [HttpPost(Name = "Login")]
        public async Task<IActionResult> Login(LoginRequestModel requestModel)
        {
            try
            {
                if (requestModel.userName == "guest")
                    _guestAccountSeeder.SeedNewData(DateTime.UtcNow.AddMonths(-1), DateTime.UtcNow);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }

            var user = await _userRepository.GetUser(requestModel.userName, requestModel.password);

            if (user is null) return BadRequest();

            LoginResponseModel? token = _jwtTokenGenerator.GenerateToken(requestModel.userName, user.Id);

            return Ok(token);
        }
    }
}
