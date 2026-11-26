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
    IAccountRepository<InvestmentAccount> investmentAccountRepository) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(List<AvailableAccount>))]
    public async Task<ActionResult<List<AvailableAccount>>> Get()
    {
        var accounts = await investmentAccountRepository.GetAvailableAccounts(ApiAuthenticationHelper.GetUserId(User))
            .ToListAsync();

        // An empty list is a normal answer for users without accounts, not a 404 logged on every page.
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
}