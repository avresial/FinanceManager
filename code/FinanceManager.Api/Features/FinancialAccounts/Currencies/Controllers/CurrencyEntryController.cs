using FinanceManager.Api.Features.Labels.Services;
using FinanceManager.Api.Shared.Helpers;
using FinanceManager.Application.Alerts.Services;
using FinanceManager.Application.Identity.Users;
using FinanceManager.Application.TransactionRules.Services;
using FinanceManager.Contracts.FinancialAccounts.Currencies;
using FinanceManager.Domain.Dashboard.Services;
using FinanceManager.Domain.FinancialAccounts.Currencies.Commands;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.FinancialAccounts.Currencies.Repositories;
using FinanceManager.Domain.FinancialAccounts.Shared.Commands;
using FinanceManager.Domain.FinancialAccounts.Shared.Entities;
using FinanceManager.Domain.FinancialAccounts.Shared.Repositories;
using FinanceManager.Domain.Identity.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinanceManager.Api.Features.FinancialAccounts.Currencies.Controllers;

[Authorize]
[Route("api/[controller]")]
[ApiController]
[Tags("Currency Entries")]
public class CurrencyEntryController(
    ICurrencyAccountRepository<CurrencyAccount> accountRepository,
    IAccountEntryRepository<CurrencyAccountEntry> accountEntryRepository) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(CurrencyAccountEntryDto))]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetEntry([FromQuery] int accountId, [FromQuery] long entryId)
    {
        var account = await accountRepository.Get(accountId);
        if (account is null || !ApiAuthenticationHelper.IsAccountOwner(User, account.UserId)) return Forbid();

        var entry = await accountEntryRepository.Get(accountId, entryId);
        if (entry is null) return NotFound();
        return Ok(entry);
    }

    [HttpGet("Youngest/{accountId:int}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(DateTime))]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetYoungestEntryDate(int accountId)
    {
        var account = await accountRepository.Get(accountId);
        if (account is null) return NotFound();
        if (!ApiAuthenticationHelper.IsAccountOwner(User, account.UserId)) return Forbid();

        var entry = await accountEntryRepository.GetYoungest(accountId);
        if (entry is null) return NotFound();
        return Ok(entry.PostingDate);
    }

    [HttpGet("Oldest/{accountId:int}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(DateTime))]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetOldestEntryDate(int accountId)
    {
        var account = await accountRepository.Get(accountId);
        if (account is null) return NotFound();
        if (!ApiAuthenticationHelper.IsAccountOwner(User, account.UserId)) return Forbid();

        var entry = await accountEntryRepository.GetOldest(accountId);
        return entry is null ? NoContent() : Ok(entry.PostingDate);
    }
}