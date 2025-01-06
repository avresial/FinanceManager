using FinanceManagerApi.Models;
using FinanceManagerApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinanceManagerApi.Controllers
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
        public async Task<ActionResult<LoginResponseModel>> Login(LoginRequestModel requestModel)
        {
            var response = jwtTokenGenerator.GenerateToken(requestModel);
            if (response is null)
                return Unauthorized();

            return response;
        }


        [Authorize]
        [HttpGet(Name = "Ping")]
        public async Task<ActionResult<bool>> Ping()
        {
            return true;
        }
    }
}
