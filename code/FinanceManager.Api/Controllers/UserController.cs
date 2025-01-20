using FinanceManager.Application.Commands;
using FinanceManager.Application.Commands.User;
using FinanceManager.Application.Providers;
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
            var encryptedPassword = PasswordEncryptionProvider.EncryptPassword(getUserCommand.password);
            var result = await _loginRepository.GetUser(getUserCommand.userName, encryptedPassword);

            if (result is not null) return Ok(result);
            return BadRequest();
        }

        [AllowAnonymous]
        [HttpPost]
        [Route("Add")]
        public async Task<IActionResult> Add(AddUser addUserCommand)
        {
            var encryptedPassword = PasswordEncryptionProvider.EncryptPassword(addUserCommand.password);
            var result = await _loginRepository.AddUser(addUserCommand.userName, encryptedPassword);

            if (result) return Ok(result);
            return BadRequest();
        }

        [Authorize]
        [HttpDelete]
        [Route("Delete")]
        public async Task<IActionResult> Delete(DeleteUser deleteUserCommand)
        {
            var idValue = User?.Claims.FirstOrDefault(c => c.Type == "userId")?.Value;
            if (idValue is null) return BadRequest();

            int id = int.Parse(idValue);
            if (id != deleteUserCommand.userId) return BadRequest();

            var result = await _loginRepository.RemoveUser(deleteUserCommand.userId);

            return Ok(result);
        }
    }
}
