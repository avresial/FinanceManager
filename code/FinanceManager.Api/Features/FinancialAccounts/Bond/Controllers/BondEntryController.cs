using FinanceManager.Api.Shared.Helpers;
using FinanceManager.Application.Alerts.Services;
using FinanceManager.Application.Identity.Users;
using FinanceManager.Contracts.FinancialAccounts.Bond;
using FinanceManager.Domain.Dashboard.Services;
using FinanceManager.Domain.FinancialAccounts.Bond.Commands;
using FinanceManager.Domain.FinancialAccounts.Bond.Entities;
using FinanceManager.Domain.FinancialAccounts.Shared.Commands;
using FinanceManager.Domain.FinancialAccounts.Shared.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinanceManager.Api.Features.FinancialAccounts.Bond.Controllers;

[Authorize]
[Route("api/[controller]")]
[ApiController]
[Tags("Bond Entries")]
public class BondEntryController(
    IAccountRepository<BondAccount> bondAccountRepository,
    IAccountEntryRepository<BondAccountEntry> bondAccountEntryRepository) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(BondAccountEntryDto))]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetEntry([FromQuery] int accountId, [FromQuery] long entryId)
    {
        var account = await bondAccountRepository.Get(accountId);
        if (account is null || !ApiAuthenticationHelper.IsAccountOwner(User, account.UserId)) return Forbid();

        var entry = await bondAccountEntryRepository.Get(accountId, entryId);
        if (entry is null) return NotFound();
        return Ok(entry);
    }

    [HttpGet("Youngest/{accountId:int}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(DateTime))]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetYoungestEntryDate(int accountId)
    {
        var account = await bondAccountRepository.Get(accountId);
        if (account is null) return NotFound();
        if (!ApiAuthenticationHelper.IsAccountOwner(User, account.UserId)) return Forbid();

        var entry = await bondAccountEntryRepository.GetYoungest(accountId);
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
        var account = await bondAccountRepository.Get(accountId);
        if (account is null) return NotFound();
        if (!ApiAuthenticationHelper.IsAccountOwner(User, account.UserId)) return Forbid();

        var entry = await bondAccountEntryRepository.GetOldest(accountId);
        return entry is null ? NoContent() : Ok(entry.PostingDate);
    }
}