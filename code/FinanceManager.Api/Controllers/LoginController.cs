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
            var user = await _userRepository.GetUser(requestModel.userName, requestModel.password);

            if (user is null)
                return Unauthorized();

            LoginResponseModel? token = _jwtTokenGenerator.GenerateToken(requestModel.userName, user.Id, requestModel.password);

            return Ok(token);
        }
    }
}
