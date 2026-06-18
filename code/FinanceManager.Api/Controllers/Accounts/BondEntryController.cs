using FinanceManager.Api.Helpers;
using FinanceManager.Application.Identity.Users;
using FinanceManager.Domain.Dashboard.Services;
using FinanceManager.Domain.FinancialAccounts.Bond.Commands;
using FinanceManager.Domain.FinancialAccounts.Bond.Dtos;
using FinanceManager.Domain.FinancialAccounts.Bond.Entities;
using FinanceManager.Domain.FinancialAccounts.Shared.Commands;
using FinanceManager.Domain.FinancialAccounts.Shared.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinanceManager.Api.Controllers.Accounts;

[Authorize]
[Route("api/[controller]")]
[ApiController]
[Tags("Bond Entries")]
public class BondEntryController(
    IAccountRepository<BondAccount> bondAccountRepository,
    IAccountEntryRepository<BondAccountEntry> bondAccountEntryRepository,
    IUserPlanVerifier userPlanVerifier,
    ICacheInvalidator dashboardCacheInvalidator) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(BondAccountEntryDto))]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetEntry([FromQuery] int accountId, [FromQuery] int entryId)
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

    [HttpPost]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(BondAccountEntryDto))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> AddEntry(AddBondAccountEntry addEntry)
    {
        var account = await bondAccountRepository.Get(addEntry.AccountId);
        if (account is null || !ApiAuthenticationHelper.IsAccountOwner(User, account.UserId)) return Forbid();
        if (!await userPlanVerifier.CanAddMoreEntries(ApiAuthenticationHelper.GetUserId(User)))
            return BadRequest("Too many entries. In order to add this entry upgrade to higher tier or delete existing one.");

        var newEntry = new BondAccountEntry(addEntry.AccountId, addEntry.EntryId,
            addEntry.PostingDate, addEntry.Value, addEntry.ValueChange, addEntry.BondDetailsId);

        await bondAccountEntryRepository.Add(newEntry);
        await dashboardCacheInvalidator.InvalidateUser(account.UserId);

        // Get all entries for this account and date to find the one we just added
        var entries = await bondAccountEntryRepository.Get(addEntry.AccountId, addEntry.PostingDate, addEntry.PostingDate.AddSeconds(1))
            .Where(e => e.BondDetailsId == addEntry.BondDetailsId)
            .OrderByDescending(x => x.EntryId)
            .ToListAsync();

        var savedEntry = entries.FirstOrDefault();
        return Ok(savedEntry);
    }

    [HttpDelete("{accountId:int}/{entryId:int}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(bool))]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> DeleteEntry(int accountId, int entryId)
    {
        var account = await bondAccountRepository.Get(accountId);
        if (account is null || !ApiAuthenticationHelper.IsAccountOwner(User, account.UserId)) return Forbid();

        var result = await bondAccountEntryRepository.Delete(accountId, entryId);
        await dashboardCacheInvalidator.InvalidateUser(account.UserId);
        return Ok(result);
    }

    [HttpPost("Recalculate/{accountId:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RecalculateBalance(int accountId)
    {
        var account = await bondAccountRepository.Get(accountId);
        if (account is null) return NotFound();
        if (!ApiAuthenticationHelper.IsAccountOwner(User, account.UserId)) return Forbid();

        await bondAccountEntryRepository.RecalculateValues(accountId);
        await dashboardCacheInvalidator.InvalidateUser(account.UserId);
        return Ok();
    }

    [HttpPut]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(BondAccountEntryDto))]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> UpdateEntry(UpdateBondAccountEntry updateEntry)
    {
        var account = await bondAccountRepository.Get(updateEntry.AccountId);
        if (account is null || !ApiAuthenticationHelper.IsAccountOwner(User, account.UserId)) return Forbid();

        var entryToUpdate = await bondAccountEntryRepository.Get(updateEntry.AccountId, updateEntry.EntryId);
        if (entryToUpdate is null) return NotFound();

        entryToUpdate.BondDetailsId = updateEntry.BondDetailsId;
        entryToUpdate.Value = updateEntry.Value;
        entryToUpdate.ValueChange = updateEntry.ValueChange;
        entryToUpdate.PostingDate = updateEntry.PostingDate;

        var result = await bondAccountEntryRepository.Update(entryToUpdate);
        await dashboardCacheInvalidator.InvalidateUser(account.UserId);
        return Ok(result);
    }
}