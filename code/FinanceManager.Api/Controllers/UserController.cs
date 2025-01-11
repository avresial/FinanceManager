using FinanceManager.Domain.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinanceManager.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class UserController(ILoginRepository loginRepository) : ControllerBase
    {
        private readonly ILoginRepository _loginRepository = loginRepository;

        [AllowAnonymous]
        [HttpPost(Name = "Add")]
        public async Task<IActionResult> Add(string login, string password)
        {
            var result = await _loginRepository.AddUser(login, password);

            if (result) return Ok();
            return BadRequest();
        }
    }
}
