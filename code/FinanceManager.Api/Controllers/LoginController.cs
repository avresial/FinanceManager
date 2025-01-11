using FinanceManager.Api.Models;
using FinanceManager.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinanceManager.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class LoginController : ControllerBase
    {
        private readonly JwtTokenGenerator jwtTokenGenerator;

        public LoginController(JwtTokenGenerator jwtTokenGenerator)
        {
            this.jwtTokenGenerator = jwtTokenGenerator;
        }

        [AllowAnonymous]
        [HttpPost(Name = "Login")]
        public async Task<IActionResult> Login(LoginRequestModel requestModel)
        {
            var response = jwtTokenGenerator.GenerateToken(requestModel);
            if (response is null)
                return Unauthorized();

            return Ok(response);
        }
    }
}
