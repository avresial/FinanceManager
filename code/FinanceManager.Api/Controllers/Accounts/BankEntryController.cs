using FinanceManager.Api.Helpers;
using FinanceManager.Application.Commands.Account;
using FinanceManager.Application.Services;
using FinanceManager.Domain.Entities.Cash;
using FinanceManager.Domain.Entities.Shared.Accounts;
using FinanceManager.Domain.Repositories.Account;
using FinanceManager.Infrastructure.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinanceManager.Api.Controllers.Accounts;

[Authorize]
[Route("api/[controller]")]
[ApiController]
[Tags("Bank Entries")]
public class BankEntryController(
    IBankAccountRepository<BankAccount> bankAccountRepository,
    IAccountEntryRepository<BankAccountEntry> bankAccountEntryRepository,
    IUserPlanVerifier userPlanVerifier) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(BankAccountEntryDto))]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetEntry([FromQuery] int accountId, [FromQuery] int entryId)
    {
        var entry = await bankAccountEntryRepository.Get(accountId, entryId);
        if (entry is null) return NotFound();
        return Ok(entry);
    }

    [HttpGet("Youngest/{accountId:int}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(DateTime))]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetYoungestEntryDate(int accountId)
    {
        var account = await bankAccountRepository.Get(accountId);
        if (account is null) return NotFound();
        if (account.UserId != ApiAuthenticationHelper.GetUserId(User)) return Forbid();

        var entry = await bankAccountEntryRepository.GetYoungest(accountId);
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
        var account = await bankAccountRepository.Get(accountId);
        if (account is null) return NotFound();
        if (account.UserId != ApiAuthenticationHelper.GetUserId(User)) return Forbid();

        var entry = await bankAccountEntryRepository.GetOldest(accountId);
        return entry is null ? NoContent() : Ok(entry.PostingDate);
    }

    [HttpPost]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(BankAccountEntryDto))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> AddEntry(AddBankAccountEntry addEntry)
    {
        if (!await userPlanVerifier.CanAddMoreEntries(ApiAuthenticationHelper.GetUserId(User)))
            return BadRequest("Too many entries. In order to add this entry upgrade to higher tier or delete existing one.");

        return Ok(await bankAccountEntryRepository.Add(new BankAccountEntry(addEntry.AccountId, addEntry.EntryId, addEntry.PostingDate, addEntry.Value, addEntry.ValueChange)
        {
            Description = addEntry.Description,
        }));
    }

    [HttpDelete("{accountId:int}/{entryId:int}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(bool))]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> DeleteEntry(int accountId, int entryId)
    {
        var account = await bankAccountRepository.Get(accountId);
        if (account is null || account.UserId != ApiAuthenticationHelper.GetUserId(User)) return Forbid();

        return Ok(await bankAccountEntryRepository.Delete(accountId, entryId));
    }

    [HttpPut]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(BankAccountEntryDto))]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> UpdateEntry(UpdateBankAccountEntry updateEntry)
    {
        var account = await bankAccountRepository.Get(updateEntry.AccountId);
        if (account is null || account.UserId != ApiAuthenticationHelper.GetUserId(User)) return Forbid();

        var newEntry = new BankAccountEntry(updateEntry.AccountId, updateEntry.EntryId, updateEntry.PostingDate, updateEntry.Value,
            updateEntry.ValueChange)
        {
            Description = updateEntry.Description
        };

        if (updateEntry.Labels is null)
            newEntry.Labels = [];
        else
            newEntry.Labels = updateEntry.Labels.Select(x => new FinancialLabel() { Name = x.Name, Id = x.Id }).ToList();

        return Ok(await bankAccountEntryRepository.Update(newEntry));
    }
}