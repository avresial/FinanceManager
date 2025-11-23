using FinanceManager.Api.Helpers;
using FinanceManager.Application.Commands.Account;
using FinanceManager.Application.Services;
using FinanceManager.Domain.Entities.Cash;
using FinanceManager.Domain.Repositories.Account;
using FinanceManager.Infrastructure.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinanceManager.Api.Controllers.Accounts;

[Authorize]
[Route("api/[controller]")]
[ApiController]
public class BankAccountController(IBankAccountRepository<BankAccount> bankAccountRepository,
    IAccountEntryRepository<BankAccountEntry> bankAccountEntryRepository, IUserPlanVerifier userPlanVerifier) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var userId = ApiAuthenticationHelper.GetUserId(User);
        var accounts = await bankAccountRepository.GetAvailableAccounts(userId).ToListAsync();

        return accounts.Count == 0 ? NotFound() : Ok(accounts);
    }

    [HttpGet("{accountId:int}")]
    public async Task<IActionResult> Get(int accountId)
    {
        var account = await bankAccountRepository.Get(accountId);
        if (account == null) return NotFound();
        if (account.UserId != ApiAuthenticationHelper.GetUserId(User)) return Forbid();

        return Ok(account);
    }

    [HttpGet("{accountId:int}&{startDate:DateTime}&{endDate:DateTime}")]
    public async Task<IActionResult> Get(int accountId, DateTime startDate, DateTime endDate)
    {
        var account = await bankAccountRepository.Get(accountId);

        if (account == null) return NotFound();
        if (account.UserId != ApiAuthenticationHelper.GetUserId(User)) return Forbid();

        var entries = bankAccountEntryRepository.Get(accountId, startDate, endDate);
        var olderEntry = await bankAccountEntryRepository.GetNextOlder(accountId, startDate);
        var youngerEntry = await bankAccountEntryRepository.GetNextYounger(accountId, endDate);

        return Ok(account.ToDto(olderEntry, youngerEntry, await entries.ToListAsync()));
    }

    [HttpPost]
    public async Task<IActionResult> Add(AddAccount addAccount)
    {
        var userId = ApiAuthenticationHelper.GetUserId(User);

        if (!await userPlanVerifier.CanAddMoreAccounts(userId))
            return BadRequest("Too many accounts. In order to add this account upgrade to higher tier or delete existing one.");

        return Ok(await bankAccountRepository.Add(userId, addAccount.accountName));
    }

    [HttpPut]
    public async Task<IActionResult> Update(UpdateAccount updateAccount)
    {
        var account = await bankAccountRepository.Get(updateAccount.accountId);

        if (account == null || account.UserId != ApiAuthenticationHelper.GetUserId(User)) return BadRequest();

        if (updateAccount.accountType is null)
            return Ok(await bankAccountRepository.Update(updateAccount.accountId, updateAccount.accountName));

        return Ok(await bankAccountRepository.Update(updateAccount.accountId, updateAccount.accountName, updateAccount.accountType.Value));
    }

    [HttpDelete("{accountId:int}")]
    public async Task<IActionResult> Delete(int accountId)
    {
        var account = await bankAccountRepository.Get(accountId);
        if (account is null || account.UserId != ApiAuthenticationHelper.GetUserId(User)) return BadRequest();

        await bankAccountEntryRepository.Delete(accountId);
        return Ok(await bankAccountRepository.Delete(accountId));
    }
}