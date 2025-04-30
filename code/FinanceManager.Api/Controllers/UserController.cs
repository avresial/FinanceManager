using FinanceManager.Application.Commands.User;
using FinanceManager.Application.Providers;
using FinanceManager.Domain.Entities.Login;
using FinanceManager.Domain.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace FinanceManager.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class UserController(IUserRepository loginRepository) : ControllerBase
    {
        private readonly IUserRepository _loginRepository = loginRepository;


        [AllowAnonymous]
        [HttpPost]
        [Route("Add")]
        public async Task<IActionResult> Add(AddUser addUserCommand)
        {
            var encryptedPassword = PasswordEncryptionProvider.EncryptPassword(addUserCommand.password);
            var result = await _loginRepository.AddUser(addUserCommand.userName, encryptedPassword, addUserCommand.pricingLevel);

            if (result) return Ok(result);
            return BadRequest();
        }

        [AllowAnonymous]
        [HttpGet]
        [Route("Get/{userId:int}")]
        public async Task<IActionResult> Get(int userId)
        {
            var result = await _loginRepository.GetUser(userId);

            if (result is not null) return Ok(result);
            return BadRequest();
        }

        [Authorize]
        [HttpGet]
        [Route("GetRecordCapacity/{userId:int}")]
        public async Task<IActionResult> GetRecordCapacity(int userId)
        {
            var result = new RecordCapacity() { TotalCapacity = 100, UsedCapacity = Random.Shared.Next(50, 100) };

            if (result is not null) return Ok(result);
            return BadRequest();
        }


        [Authorize]
        [HttpDelete]
        [Route("Delete/{userId:int}")]
        public async Task<IActionResult> Delete(int userId)
        {
            var idValue = User?.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;
            if (idValue is null) return BadRequest();

            int id = int.Parse(idValue);
            if (id != userId) return BadRequest();

            var result = await _loginRepository.RemoveUser(userId);

            return Ok(result);
        }

        [Authorize]
        [HttpPut]
        [Route("UpdatePassword")]
        public async Task<IActionResult> UpdatePassword(UpdatePassword updatePassword)
        {
            return BadRequest();
        }

        [Authorize]
        [HttpPut]
        [Route("UpdatePricingPlan")]
        public async Task<IActionResult> UpdatePricingPlan(UpdatePricingPlan updatePricingPlan)
        {
            return BadRequest();
        }


    }
}
