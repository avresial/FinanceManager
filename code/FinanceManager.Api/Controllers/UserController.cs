using FinanceManager.Api.Models;
using FinanceManager.Application.Commands;
using FinanceManager.Domain.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinanceManager.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class UserController(IUserRepository loginRepository) : ControllerBase
    {
        private readonly IUserRepository _loginRepository = loginRepository;

        [AllowAnonymous]
        [HttpGet]
        [Route("Get")]
        public async Task<IActionResult> Get(GetUser getUserCommand)
        {
            var result = await _loginRepository.GetUser(getUserCommand.userName, getUserCommand.password);

            if (result is not null) return Ok(result);
            return BadRequest();
        }

        [AllowAnonymous]
        [HttpPost]
        [Route("Add")]
        public async Task<IActionResult> Add(AddUser addUserCommand)
        {
            var result = await _loginRepository.AddUser(addUserCommand.userName, addUserCommand.password);

            if (result) return Ok();
            return BadRequest();
        }

        [HttpDelete]
        [Route("Delete")]
        public async Task<IActionResult> Delete(DeleteUser deleteUserCommand)
        {
            var result = await _loginRepository.RemoveUser(deleteUserCommand.userId);

            return Ok(result);
        }
    }
}
