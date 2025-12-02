using FinanceManager.Api.Helpers;
using FinanceManager.Application.Commands.Account;
using FinanceManager.Application.Services;
using FinanceManager.Domain.Entities.Bonds;
using FinanceManager.Domain.Repositories.Account;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinanceManager.Api.Controllers.Accounts;

[Authorize]
[Route("api/[controller]")]
[ApiController]
public class BondEntryController(
    IAccountRepository<BondAccount> bondAccountRepository,
    IAccountEntryRepository<BondAccountEntry> bondAccountEntryRepository,
    IUserPlanVerifier userPlanVerifier) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetEntry([FromQuery] int accountId, [FromQuery] int entryId)
    {
        var entry = await bondAccountEntryRepository.Get(accountId, entryId);
        if (entry is null) return NotFound();
        return Ok(entry);
    }

    [HttpGet("Youngest/{accountId:int}")]
    public async Task<IActionResult> GetYoungestEntryDate(int accountId)
    {
        var account = await bondAccountRepository.Get(accountId);
        if (account is null) return NotFound();
        if (account.UserId != ApiAuthenticationHelper.GetUserId(User)) return Forbid();

        var entry = await bondAccountEntryRepository.GetYoungest(accountId);
        if (entry is null) return NotFound();
        return Ok(entry.PostingDate);
    }

    [HttpGet("Oldest/{accountId:int}")]
    public async Task<IActionResult> GetOldestEntryDate(int accountId)
    {
        var account = await bondAccountRepository.Get(accountId);
        if (account is null) return NotFound();
        if (account.UserId != ApiAuthenticationHelper.GetUserId(User)) return Forbid();

        var entry = await bondAccountEntryRepository.GetOldest(accountId);
        return entry is null ? NoContent() : Ok(entry.PostingDate);
    }

    [HttpPost]
    public async Task<IActionResult> AddEntry(AddBondAccountEntry addEntry)
    {
        if (!await userPlanVerifier.CanAddMoreEntries(ApiAuthenticationHelper.GetUserId(User)))
            return BadRequest("Too many entries. In order to add this entry upgrade to higher tier or delete existing one.");

        return Ok(await bondAccountEntryRepository.Add(new(addEntry.AccountId, addEntry.EntryId,
        addEntry.PostingDate, addEntry.Value, addEntry.ValueChange, addEntry.BondDetailsId)));
    }

    [HttpDelete("{accountId:int}/{entryId:int}")]
    public async Task<IActionResult> DeleteEntry(int accountId, int entryId)
    {
        var account = await bondAccountRepository.Get(accountId);
        if (account is null || account.UserId != ApiAuthenticationHelper.GetUserId(User)) return Forbid();

        return Ok(await bondAccountEntryRepository.Delete(accountId, entryId));
    }

    [HttpPut]
    public async Task<IActionResult> UpdateEntry(UpdateBondAccountEntry updateEntry)
    {
        var account = await bondAccountRepository.Get(updateEntry.AccountId);
        if (account is null || account.UserId != ApiAuthenticationHelper.GetUserId(User)) return Forbid();

        var entryToUpdate = await bondAccountEntryRepository.Get(updateEntry.AccountId, updateEntry.EntryId);
        if (entryToUpdate is null) return NotFound();

        entryToUpdate.BondDetailsId = updateEntry.BondDetailsId;
        entryToUpdate.Value = updateEntry.Value;
        entryToUpdate.ValueChange = updateEntry.ValueChange;
        entryToUpdate.PostingDate = updateEntry.PostingDate;

        return Ok(await bondAccountEntryRepository.Update(entryToUpdate));
    }
}