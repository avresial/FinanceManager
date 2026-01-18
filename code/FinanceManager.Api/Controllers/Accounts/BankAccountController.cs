using FinanceManager.Api.Helpers;
using FinanceManager.Application.Commands.Account;
using FinanceManager.Application.Services;
using FinanceManager.Domain.Dtos;
using FinanceManager.Domain.Entities.FinancialAccounts.Currency;
using FinanceManager.Domain.Repositories.Account;
using FinanceManager.Infrastructure.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinanceManager.Api.Controllers.Accounts;

[Authorize]
[Route("api/[controller]")]
[ApiController]
[Tags("Bank Accounts")]
public class BankAccountController(ICurrencyAccountRepository<CurrencyAccount> bankAccountRepository,
    IAccountEntryRepository<CurrencyAccountEntry> bankAccountEntryRepository, IUserPlanVerifier userPlanVerifier) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(List<CurrencyAccountDto>))]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get()
    {
        var userId = ApiAuthenticationHelper.GetUserId(User);
        var accounts = await bankAccountRepository.GetAvailableAccounts(userId).ToListAsync();

        return accounts.Count == 0 ? NotFound() : Ok(accounts);
    }

    [HttpGet("{accountId:int}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(CurrencyAccountDto))]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Get(int accountId)
    {
        var account = await bankAccountRepository.Get(accountId);
        if (account is null) return NotFound();
        if (account.UserId != ApiAuthenticationHelper.GetUserId(User)) return Forbid();

        return Ok(account);
    }

    [HttpGet("{accountId:int}&{startDate:DateTime}&{endDate:DateTime}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(CurrencyAccountDto))]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Get(int accountId, DateTime startDate, DateTime endDate)
    {
        var account = await bankAccountRepository.Get(accountId);

        if (account is null) return NotFound();
        if (account.UserId != ApiAuthenticationHelper.GetUserId(User)) return Forbid();

        var entries = bankAccountEntryRepository.Get(accountId, startDate, endDate);
        var olderEntry = await bankAccountEntryRepository.GetNextOlder(accountId, startDate);
        var youngerEntry = await bankAccountEntryRepository.GetNextYounger(accountId, endDate);

        return Ok(account.ToDto(olderEntry, youngerEntry, await entries.ToListAsync()));
    }

    [HttpPost]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(int))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Add(AddAccount addAccount)
    {
        var userId = ApiAuthenticationHelper.GetUserId(User);

        if (!await userPlanVerifier.CanAddMoreAccounts(userId))
            return BadRequest("Too many accounts. In order to add this account upgrade to higher tier or delete existing one.");

        return Ok(await bankAccountRepository.Add(userId, addAccount.accountName));
    }

    [HttpPut]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(int))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Update(UpdateAccount updateAccount)
    {
        var account = await bankAccountRepository.Get(updateAccount.AccountId);

        if (account is null || account.UserId != ApiAuthenticationHelper.GetUserId(User)) return BadRequest();
        return Ok(await bankAccountRepository.Update(updateAccount.AccountId, updateAccount.AccountName, updateAccount.AccountType));
    }

    [HttpDelete("{accountId:int}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(bool))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Delete(int accountId)
    {
        var account = await bankAccountRepository.Get(accountId);
        if (account is null || account.UserId != ApiAuthenticationHelper.GetUserId(User)) return BadRequest();

        await bankAccountEntryRepository.Delete(accountId);
        return Ok(await bankAccountRepository.Delete(accountId));
    }
}