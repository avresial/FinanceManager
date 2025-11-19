using FinanceManager.Api.Helpers;
using FinanceManager.Application.Commands.Account;
using FinanceManager.Domain.Entities.Accounts;
using FinanceManager.Domain.Entities.Accounts.Entries;
using FinanceManager.Domain.Repositories.Account;
using FinanceManager.Infrastructure.Dtos;
using FinanceManager.Infrastructure.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinanceManager.Api.Controllers.Accounts;

[Authorize]
[Route("api/[controller]")]
[ApiController]
public class StockAccountController(IAccountRepository<StockAccount> stockAccountRepository,
    IStockAccountEntryRepository<StockAccountEntry> stockAccountEntryRepository) : ControllerBase
{

    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var accounts = await stockAccountRepository.GetAvailableAccounts(ApiAuthenticationHelper.GetUserId(User))
            .ToListAsync();
        if (accounts.Count == 0) return NotFound();

        return Ok(accounts);
    }

    [HttpGet("{accountId:int}")]
    public async Task<IActionResult> Get(int accountId)
    {
        var account = await stockAccountRepository.Get(accountId);
        if (account == null) return NoContent();
        if (account.UserId != ApiAuthenticationHelper.GetUserId(User))
            return Forbid("User does not own this account.");

        return Ok(account);
    }

    [HttpGet("{accountId:int}&{startDate:DateTime}&{endDate:DateTime}")]
    public async Task<IActionResult> Get(int accountId, DateTime startDate, DateTime endDate)
    {
        var userId = ApiAuthenticationHelper.GetUserId(User);

        var account = await stockAccountRepository.Get(accountId);
        if (account == null) return NotFound();
        if (account.UserId != userId) return Forbid("User ID does not match the account owner.");

        var entries = await stockAccountEntryRepository.Get(accountId, startDate, endDate).ToListAsync();

        return Ok(new StockAccountDto()
        {
            AccountId = account.AccountId,
            UserId = account.UserId,
            Name = account.Name,
            NextOlderEntries = (await stockAccountEntryRepository.GetNextOlder(accountId, startDate)).ToDictionary(x => x.Key, x => x.Value.ToDto()),
            NextYoungerEntries = (await stockAccountEntryRepository.GetNextYounger(accountId, startDate)).ToDictionary(x => x.Key, x => x.Value.ToDto()),
            Entries = entries.Select(x => x.ToDto())
        });
    }

    [HttpGet("GetYoungestEntryDate/{accountId:int}")]
    public async Task<IActionResult> GetYoungestEntryDate(int accountId)
    {
        var account = await stockAccountRepository.Get(accountId);

        if (account == null) return NotFound();
        if (account.UserId != ApiAuthenticationHelper.GetUserId(User)) return Forbid();

        var entry = await stockAccountEntryRepository.GetYoungest(accountId);
        if (entry is not null)
            return Ok(entry.PostingDate);

        return NoContent();
    }

    [HttpGet("GetOldestEntryDate/{accountId:int}")]
    public async Task<IActionResult> GetOldestEntryDate(int accountId)
    {
        var account = await stockAccountRepository.Get(accountId);

        if (account == null) return NoContent();
        if (account.UserId != ApiAuthenticationHelper.GetUserId(User)) return Forbid();

        var entry = await stockAccountEntryRepository.GetOldest(accountId);
        if (entry is not null)
            return Ok(entry.PostingDate);

        return NotFound();
    }

    [HttpPost("Add")]
    public async Task<IActionResult> Add(AddAccount addAccount) =>
        Ok(await stockAccountRepository.Add(ApiAuthenticationHelper.GetUserId(User), addAccount.accountName));

    [HttpPost("AddEntry")]
    public async Task<IActionResult> AddEntry(AddStockAccountEntry addEntry) =>
        Ok(await stockAccountEntryRepository.Add(addEntry.entry));

    [HttpPut("Update")]
    public async Task<IActionResult> Update(UpdateAccount updateAccount)
    {
        var account = await stockAccountRepository.Get(updateAccount.accountId);
        if (account is null || account.UserId != ApiAuthenticationHelper.GetUserId(User))
            return Forbid("User ID does not match the account owner.");

        var result = await stockAccountRepository.Update(updateAccount.accountId, updateAccount.accountName);
        return Ok(result);
    }


    [HttpDelete("Delete/{accountId:int}")]
    public async Task<IActionResult> Delete(int accountId)
    {
        var account = await stockAccountRepository.Get(accountId);
        if (account == null) return NotFound("Account not found.");
        if (account.UserId != ApiAuthenticationHelper.GetUserId(User))
            return Forbid("User does not own this account.");

        await stockAccountEntryRepository.Delete(accountId);
        return Ok(await stockAccountRepository.Delete(accountId));
    }

    [HttpDelete("DeleteEntry/{accountId:int}/{entryId:int}")]
    public async Task<IActionResult> DeleteEntry(int accountId, int entryId)
    {
        var account = await stockAccountRepository.Get(accountId);
        if (account is null || account.UserId != ApiAuthenticationHelper.GetUserId(User))
            return Forbid("User ID does not match the account owner.");

        return Ok(await stockAccountEntryRepository.Delete(accountId, entryId));
    }

    [HttpPut("UpdateEntry")]
    public async Task<IActionResult> UpdateEntry(UpdateStockAccountEntry updateCommand)
    {
        var account = await stockAccountRepository.Get(updateCommand.AccountId);
        if (account == null || account.UserId != ApiAuthenticationHelper.GetUserId(User))
            return Forbid("User ID does not match the account owner.");

        var entryToUpdate = await stockAccountEntryRepository.Get(updateCommand.AccountId, updateCommand.EntryId);

        if (entryToUpdate is null) return NotFound();

        entryToUpdate.Update(new(updateCommand.AccountId, updateCommand.EntryId, updateCommand.PostingDate, updateCommand.Value,
            updateCommand.ValueChange, updateCommand.Ticker, updateCommand.investmentType));

        return Ok(await stockAccountEntryRepository.Update(entryToUpdate));
    }
}
