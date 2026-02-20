using FinanceManager.Api.Helpers;
using FinanceManager.Api.Services;
using FinanceManager.Application.Services;
using FinanceManager.Domain.Commands.Account;
using FinanceManager.Domain.Dtos;
using FinanceManager.Domain.Entities.FinancialAccounts.Currencies;
using FinanceManager.Domain.Entities.Shared.Accounts;
using FinanceManager.Domain.Repositories.Account;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinanceManager.Api.Controllers.Accounts;

[Authorize]
[Route("api/[controller]")]
[ApiController]
[Tags("Currency Entries")]
public class CurrencyEntryController(
    ICurrencyAccountRepository<CurrencyAccount> accountRepository,
    IAccountEntryRepository<CurrencyAccountEntry> accountEntryRepository,
    IUserPlanVerifier userPlanVerifier, ILabelSetterChannel labelSetterChannel) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(CurrencyAccountEntryDto))]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetEntry([FromQuery] int accountId, [FromQuery] int entryId)
    {
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
        if (account.UserId != ApiAuthenticationHelper.GetUserId(User)) return Forbid();

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
        if (account.UserId != ApiAuthenticationHelper.GetUserId(User)) return Forbid();

        var entry = await accountEntryRepository.GetOldest(accountId);
        return entry is null ? NoContent() : Ok(entry.PostingDate);
    }

    [HttpPost]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(CurrencyAccountEntryDto))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> AddEntry(AddCurrencyAccountEntry addEntry)
    {
        if (!await userPlanVerifier.CanAddMoreEntries(ApiAuthenticationHelper.GetUserId(User)))
            return BadRequest("Too many entries. In order to add this entry upgrade to higher tier or delete existing one.");


        var newEntry = new CurrencyAccountEntry(addEntry.AccountId, 0, addEntry.PostingDate, addEntry.Value, addEntry.ValueChange)
        {
            Description = addEntry.Description,
            ContractorDetails = addEntry.ContractorDetails
        };

        var result = await accountEntryRepository.Add(newEntry);
        await labelSetterChannel.QueueEntries(newEntry.AccountId, [newEntry.EntryId]);

        return Ok(result);
    }

    [HttpDelete("{accountId:int}/{entryId:int}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(bool))]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> DeleteEntry(int accountId, int entryId)
    {
        var account = await accountRepository.Get(accountId);
        if (account is null || account.UserId != ApiAuthenticationHelper.GetUserId(User)) return Forbid();

        return Ok(await accountEntryRepository.Delete(accountId, entryId));
    }

    [HttpPut]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(CurrencyAccountEntryDto))]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> UpdateEntry(UpdateCurrencyAccountEntry updateEntry)
    {
        var account = await accountRepository.Get(updateEntry.AccountId);
        if (account is null || account.UserId != ApiAuthenticationHelper.GetUserId(User)) return Forbid();

        var newEntry = new CurrencyAccountEntry(updateEntry.AccountId, updateEntry.EntryId, updateEntry.PostingDate, updateEntry.Value,
            updateEntry.ValueChange)
        {
            Description = updateEntry.Description,
            ContractorDetails = updateEntry.ContractorDetails
        };

        if (updateEntry.Labels is null)
            newEntry.Labels = [];
        else
            newEntry.Labels = updateEntry.Labels.Select(x => new FinancialLabel() { Name = x.Name, Id = x.Id }).ToList();

        return Ok(await accountEntryRepository.Update(newEntry));
    }
}