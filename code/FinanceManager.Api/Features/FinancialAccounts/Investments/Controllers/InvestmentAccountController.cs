using FinanceManager.Api.Shared.Helpers;
using FinanceManager.Domain.Dashboard.Services;
using FinanceManager.Domain.FinancialAccounts.Investments.Entities;
using FinanceManager.Domain.FinancialAccounts.Shared.Commands;
using FinanceManager.Domain.FinancialAccounts.Shared.Repositories;
using FinanceManager.Domain.FinancialAccounts.Shared.ValueObjects;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinanceManager.Api.Features.FinancialAccounts.Investments.Controllers;

[Authorize]
[Route("api/[controller]")]
[ApiController]
[Tags("Investment Accounts")]
public class InvestmentAccountController(
    IAccountRepository<InvestmentAccount> investmentAccountRepository,
    ICacheInvalidator dashboardCacheInvalidator) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(List<AvailableAccount>))]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<List<AvailableAccount>>> Get()
    {
        var accounts = await investmentAccountRepository.GetAvailableAccounts(ApiAuthenticationHelper.GetUserId(User))
            .ToListAsync();

        if (accounts.Count == 0) return NotFound();

        return Ok(accounts);
    }

    [HttpGet("{accountId:int}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(InvestmentAccount))]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Get(int accountId)
    {
        var account = await investmentAccountRepository.Get(accountId);
        if (account is null) return NoContent();
        if (!ApiAuthenticationHelper.IsAccountOwner(User, account.UserId))
            return Forbid();

        return Ok(account);
    }

    [HttpPost("Add")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Add(AddAccount addAccount)
    {
        var userId = ApiAuthenticationHelper.GetUserId(User);
        var result = await investmentAccountRepository.Add(userId, addAccount.AccountName);
        await dashboardCacheInvalidator.InvalidateUser(userId);
        return Ok(result);
    }

    [HttpPut("Update")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Update(UpdateAccount updateAccount)
    {
        var account = await investmentAccountRepository.Get(updateAccount.AccountId);
        if (account is null || !ApiAuthenticationHelper.IsAccountOwner(User, account.UserId))
            return Forbid();

        var result = await investmentAccountRepository.Update(updateAccount.AccountId, updateAccount.AccountName);
        await dashboardCacheInvalidator.InvalidateUser(account.UserId);
        return Ok(result);
    }

    [HttpDelete("Delete/{accountId:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int accountId)
    {
        var account = await investmentAccountRepository.Get(accountId);
        if (account is null) return NotFound("Account not found.");
        if (!ApiAuthenticationHelper.IsAccountOwner(User, account.UserId))
            return Forbid();

        var result = await investmentAccountRepository.Delete(accountId);
        await dashboardCacheInvalidator.InvalidateUser(account.UserId);
        return Ok(result);
    }
}