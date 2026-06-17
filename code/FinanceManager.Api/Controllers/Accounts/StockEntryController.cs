using FinanceManager.Api.Helpers;
using FinanceManager.Domain.FinancialAccounts.Shared.Commands;
using FinanceManager.Domain.FinancialAccounts.Shared.Repositories;
using FinanceManager.Domain.FinancialAccounts.Stock.Commands;
using FinanceManager.Domain.FinancialAccounts.Stock.Dtos;
using FinanceManager.Domain.FinancialAccounts.Stock.Entities;
using FinanceManager.Domain.FinancialAccounts.Stock.Repositories;
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
        if (!ApiAuthenticationHelper.IsAccountOwner(User, account.UserId)) return Forbid();

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
        if (!ApiAuthenticationHelper.IsAccountOwner(User, account.UserId)) return Forbid();

        var entry = await stockAccountEntryRepository.GetOldest(accountId);
        if (entry is not null)
            return Ok(entry.PostingDate);

        return NotFound();
    }

    [HttpPost("Add")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(StockAccountEntryDto))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Add(AddStockAccountEntry addEntry)
    {
        var account = await stockAccountRepository.Get(addEntry.Entry.AccountId);
        if (account is null || !ApiAuthenticationHelper.IsAccountOwner(User, account.UserId))
            return Forbid();

        return Ok(await stockAccountEntryRepository.Add(addEntry.Entry));
    }

    [HttpDelete("Delete/{accountId:int}/{entryId:int}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(bool))]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Delete(int accountId, int entryId)
    {
        var account = await stockAccountRepository.Get(accountId);
        if (account is null || !ApiAuthenticationHelper.IsAccountOwner(User, account.UserId))
            return Forbid();

        return Ok(await stockAccountEntryRepository.Delete(accountId, entryId));
    }

    [HttpPost("Recalculate/{accountId:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RecalculateBalance(int accountId)
    {
        var account = await stockAccountRepository.Get(accountId);
        if (account is null) return NotFound();
        if (!ApiAuthenticationHelper.IsAccountOwner(User, account.UserId)) return Forbid();

        await stockAccountEntryRepository.RecalculateValues(accountId);
        return Ok();
    }

    [HttpPut("Update")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(StockAccountEntryDto))]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Update(UpdateStockAccountEntry updateCommand)
    {
        var account = await stockAccountRepository.Get(updateCommand.AccountId);
        if (account is null || !ApiAuthenticationHelper.IsAccountOwner(User, account.UserId))
            return Forbid();

        var entryToUpdate = await stockAccountEntryRepository.Get(updateCommand.AccountId, updateCommand.EntryId);

        if (entryToUpdate is null) return NotFound();

        entryToUpdate.Update(new StockAccountEntry(updateCommand.AccountId, updateCommand.EntryId, updateCommand.PostingDate, updateCommand.Value,
            updateCommand.ValueChange, entryToUpdate.Isin, updateCommand.InvestmentType)
        {
            Ticker = updateCommand.Ticker
        });

        return Ok(await stockAccountEntryRepository.Update(entryToUpdate));
    }
}