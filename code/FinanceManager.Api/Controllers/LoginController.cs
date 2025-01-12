using FinanceManager.Api.Models;
using FinanceManager.Api.Services;
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
        public LoginController(JwtTokenGenerator jwtTokenGenerator, IUserRepository userRepository)
        {
            _jwtTokenGenerator = jwtTokenGenerator;
            _userRepository = userRepository;
        }

        [AllowAnonymous]
        [HttpPost(Name = "Login")]
        public async Task<IActionResult> Login(LoginRequestModel requestModel)
        {
            var user = await _userRepository.GetUser(requestModel.UserName, requestModel.Password);

            if (user is null)
                return Unauthorized();

            LoginResponseModel token = _jwtTokenGenerator.GenerateToken(new LoginRequestModel()
            {
                UserName = requestModel.UserName,
                Password = requestModel.Password,
            });

            return Ok(token);
        }
    }
}
