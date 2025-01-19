using FinanceManager.Api.Helpers;
using FinanceManager.Application.Commands.Account;
using FinanceManager.Domain.Repositories.Account;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinanceManager.Api.Controllers.Accounts;

[Authorize]
[Route("api/[controller]")]
[ApiController]
public class BankAccountController(IBankAccountRepository bankAccountRepository) : ControllerBase
{
    private readonly IBankAccountRepository bankAccountRepository = bankAccountRepository;

    [HttpGet("{accountId:int}")]
    public async Task<IActionResult> Get(int accountId)
    {
        var userId = ApiAuthenticationHelper.GetUserId(User);
        if (userId is null) return BadRequest();

        var account = bankAccountRepository.Get(accountId);

        if (account == null) return NoContent();
        if (account.Id != userId) return BadRequest();

        return Ok(account);
    }

    [HttpPost]
    [Route("Add")]
    public async Task<IActionResult> Add(AddAccount addAccount)
    {
        var userId = ApiAuthenticationHelper.GetUserId(User);
        if (!userId.HasValue) return BadRequest();

        return Ok(bankAccountRepository.Add(userId.Value, addAccount.accountName));
    }

    [HttpPut]
    [Route("Update")]
    public async Task<IActionResult> Update(UpdateAccount updateAccount)
    {
        var userId = ApiAuthenticationHelper.GetUserId(User);
        if (userId is null) return BadRequest();

        var account = bankAccountRepository.Get(updateAccount.accountId);

        if (account == null || account.Id != userId) return BadRequest();
        return Ok(bankAccountRepository.Update(updateAccount.accountId, updateAccount.accountName));
    }


    [HttpDelete]
    [Route("Delete")]
    public async Task<IActionResult> Delete(DeleteAccount deleteAccount)
    {
        var userId = ApiAuthenticationHelper.GetUserId(User);
        if (userId is null) return BadRequest();

        var account = bankAccountRepository.Get(deleteAccount.accountId);

        if (account == null || account.Id != userId) return BadRequest();
        return Ok(bankAccountRepository.Delete(deleteAccount.accountId));
    }
}
