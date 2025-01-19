using FinanceManager.Api.Helpers;
using FinanceManager.Application.Commands.Account;
using FinanceManager.Domain.Entities.Accounts;
using FinanceManager.Domain.Repositories.Account;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinanceManager.Api.Controllers.Accounts;

[Authorize]
[Route("api/[controller]")]
[ApiController]
public class BankAccountController(IAccountRepository<BankAccount> bankAccountRepository) : ControllerBase
{
    private readonly IAccountRepository<BankAccount> bankAccountRepository = bankAccountRepository;

    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var userId = ApiAuthenticationHelper.GetUserId(User);
        if (userId is null) return BadRequest();

        var account = bankAccountRepository.GetAvailableAccounts(userId.Value);

        if (account == null) return NoContent();

        return Ok(account);
    }


    [HttpGet("{accountId:int}")]
    public async Task<IActionResult> Get(int accountId)
    {
        var userId = ApiAuthenticationHelper.GetUserId(User);
        if (userId is null) return BadRequest();

        var account = bankAccountRepository.Get(accountId);

        if (account == null) return NoContent();
        if (account.UserId != userId) return BadRequest();

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

        if (account == null || account.UserId != userId) return BadRequest();
        return Ok(bankAccountRepository.Update(updateAccount.accountId, updateAccount.accountName));
    }


    [HttpDelete]
    [Route("Delete")]
    public async Task<IActionResult> Delete(DeleteAccount deleteAccount)
    {
        var userId = ApiAuthenticationHelper.GetUserId(User);
        if (userId is null) return BadRequest();

        var account = bankAccountRepository.Get(deleteAccount.accountId);

        if (account == null || account.UserId != userId) return BadRequest();
        return Ok(bankAccountRepository.Delete(deleteAccount.accountId));
    }
}
