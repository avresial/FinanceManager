using FinanceManager.Api.Helpers;
using FinanceManager.Application.Commands.Account;
using FinanceManager.Domain.Entities.Stocks;
using FinanceManager.Domain.Repositories.Account;
using FinanceManager.Infrastructure.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinanceManager.Api.Controllers.Accounts;

[Authorize]
[Route("api/[controller]")]
[ApiController]
[Tags("Stock Entries")]
public class StockEntryController(
    IAccountRepository<StockAccount> stockAccountRepository,
    IStockAccountEntryRepository<StockAccountEntry> stockAccountEntryRepository) : ControllerBase
{
    [HttpGet("GetYoungestEntryDate/{accountId:int}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(DateTime))]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetYoungestEntryDate(int accountId)
    {
        var account = await stockAccountRepository.Get(accountId);

        if (account is null) return NotFound();
        if (account.UserId != ApiAuthenticationHelper.GetUserId(User)) return Forbid();

        var entry = await stockAccountEntryRepository.GetYoungest(accountId);
        if (entry is not null)
            return Ok(entry.PostingDate);

        return NoContent();
    }

    [HttpGet("GetOldestEntryDate/{accountId:int}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(DateTime))]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetOldestEntryDate(int accountId)
    {
        var account = await stockAccountRepository.Get(accountId);

        if (account is null) return NotFound();
        if (account.UserId != ApiAuthenticationHelper.GetUserId(User)) return Forbid();

        var entry = await stockAccountEntryRepository.GetOldest(accountId);
        if (entry is not null)
            return Ok(entry.PostingDate);

        return NotFound();
    }

    [HttpPost("Add")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(StockAccountEntryDto))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Add(AddStockAccountEntry addEntry) =>
        Ok(await stockAccountEntryRepository.Add(addEntry.entry));

    [HttpDelete("Delete/{accountId:int}/{entryId:int}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(bool))]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Delete(int accountId, int entryId)
    {
        var account = await stockAccountRepository.Get(accountId);
        if (account is null || account.UserId != ApiAuthenticationHelper.GetUserId(User))
            return Forbid("User ID does not match the account owner.");

        return Ok(await stockAccountEntryRepository.Delete(accountId, entryId));
    }

    [HttpPut("Update")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(StockAccountEntryDto))]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Update(UpdateStockAccountEntry updateCommand)
    {
        var account = await stockAccountRepository.Get(updateCommand.AccountId);
        if (account is null || account.UserId != ApiAuthenticationHelper.GetUserId(User))
            return Forbid("User ID does not match the account owner.");

        var entryToUpdate = await stockAccountEntryRepository.Get(updateCommand.AccountId, updateCommand.EntryId);

        if (entryToUpdate is null) return NotFound();

        entryToUpdate.Update(new(updateCommand.AccountId, updateCommand.EntryId, updateCommand.PostingDate, updateCommand.Value,
            updateCommand.ValueChange, updateCommand.Ticker, updateCommand.investmentType));

        return Ok(await stockAccountEntryRepository.Update(entryToUpdate));
    }
}