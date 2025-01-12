using FinanceManager.Api.Models;
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
        public async Task<IActionResult> Get(UserRequestModel requestModel)
        {
            var result = await _loginRepository.GetUser(requestModel.UserName, requestModel.Password);

            if (result is not null) return Ok(result);
            return BadRequest();
        }

        [AllowAnonymous]
        [HttpPost]
        [Route("Add")]
        public async Task<IActionResult> Add(UserRequestModel requestModel)
        {
            var result = await _loginRepository.AddUser(requestModel.UserName, requestModel.Password);

            if (result) return Ok();
            return BadRequest();
        }

        [HttpDelete]
        [Route("Delete")]
        public async Task<IActionResult> Delete(UserRequestModel requestModel)
        {
            var user = await _loginRepository.GetUser(requestModel.UserName, requestModel.Password);

            if (user is null) return BadRequest(user);

            var result = await _loginRepository.RemoveUser(user.Id);

            return Ok(result);
        }
    }
}
