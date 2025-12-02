using FinanceManager.Api.Helpers;
using FinanceManager.Application.Commands.Account;
using FinanceManager.Application.Services;
using FinanceManager.Domain.Entities.Bonds;
using FinanceManager.Domain.Repositories.Account;
using FinanceManager.Infrastructure.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinanceManager.Api.Controllers.Accounts;

[Authorize]
[Route("api/[controller]")]
[ApiController]
public class BondAccountController(IAccountRepository<BondAccount> bondAccountRepository,
    IAccountEntryRepository<BondAccountEntry> bondAccountEntryRepository, IUserPlanVerifier userPlanVerifier) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var userId = ApiAuthenticationHelper.GetUserId(User);
        var accounts = await bondAccountRepository.GetAvailableAccounts(userId).ToListAsync();

        return accounts.Count == 0 ? NotFound() : Ok(accounts);
    }

    [HttpGet("{accountId:int}")]
    public async Task<IActionResult> Get(int accountId)
    {
        var account = await bondAccountRepository.Get(accountId);
        if (account is null) return NotFound();
        if (account.UserId != ApiAuthenticationHelper.GetUserId(User)) return Forbid();

        return Ok(account);
    }

    [HttpGet("{accountId:int}&{startDate:DateTime}&{endDate:DateTime}")]
    public async Task<IActionResult> Get(int accountId, DateTime startDate, DateTime endDate)
    {
        var account = await bondAccountRepository.Get(accountId);

        if (account is null) return NotFound();
        if (account.UserId != ApiAuthenticationHelper.GetUserId(User)) return Forbid();

        var entries = bondAccountEntryRepository.Get(accountId, startDate, endDate);
        var olderEntry = await bondAccountEntryRepository.GetNextOlder(accountId, startDate);
        var youngerEntry = await bondAccountEntryRepository.GetNextYounger(accountId, endDate);

        return Ok(account.ToDto(olderEntry, youngerEntry, await entries.ToListAsync()));
    }

    [HttpPost]
    public async Task<IActionResult> Add(AddBondAccount addAccount)
    {
        var userId = ApiAuthenticationHelper.GetUserId(User);

        if (!await userPlanVerifier.CanAddMoreAccounts(userId))
            return BadRequest("Too many accounts. In order to add this account upgrade to higher tier or delete existing one.");

        return Ok(await bondAccountRepository.Add(userId, addAccount.accountName));
    }

    [HttpPut]
    public async Task<IActionResult> Update(UpdateAccount updateAccount)
    {
        var account = await bondAccountRepository.Get(updateAccount.AccountId);

        if (account is null || account.UserId != ApiAuthenticationHelper.GetUserId(User)) return BadRequest();

        return Ok(await bondAccountRepository.Update(updateAccount.AccountId, updateAccount.AccountName));
    }

    [HttpDelete("{accountId:int}")]
    public async Task<IActionResult> Delete(int accountId)
    {
        var account = await bondAccountRepository.Get(accountId);
        if (account is null || account.UserId != ApiAuthenticationHelper.GetUserId(User)) return BadRequest();

        await bondAccountEntryRepository.Delete(accountId);
        return Ok(await bondAccountRepository.Delete(accountId));
    }
}