using FinanceManager.Api.Helpers;
using FinanceManager.Application.Commands.Account;
using FinanceManager.Application.Services;
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
public class BankAccountController(IBankAccountRepository<BankAccount> bankAccountRepository,
IAccountEntryRepository<BankAccountEntry> bankAccountEntryRepository, UserPlanVerifier userPlanVerifier) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var userId = ApiAuthenticationHelper.GetUserId(User);
        if (userId is null) return BadRequest();

        var account = await bankAccountRepository.GetAvailableAccounts(userId.Value);

        if (account == null) return NoContent();

        return await Task.FromResult(Ok(account));
    }

    [HttpGet("{accountId:int}")]
    public async Task<IActionResult> Get(int accountId)
    {
        var userId = ApiAuthenticationHelper.GetUserId(User);
        if (userId is null) return BadRequest();

        var account = await bankAccountRepository.Get(accountId);
        if (account == null) return NoContent();
        if (account.UserId != userId) return BadRequest();

        return await Task.FromResult(Ok(account));
    }

    [HttpGet("{accountId:int}&{startDate:DateTime}&{endDate:DateTime}")]
    public async Task<IActionResult> Get(int accountId, DateTime startDate, DateTime endDate)
    {
        var userId = ApiAuthenticationHelper.GetUserId(User);
        if (userId is null) return BadRequest();

        var account = await bankAccountRepository.Get(accountId);

        if (account == null) return NoContent();
        if (account.UserId != userId) return BadRequest();

        IEnumerable<BankAccountEntry> entries = await bankAccountEntryRepository.Get(accountId, startDate, endDate);

        var olderEntry = await bankAccountEntryRepository.GetNextOlder(accountId, startDate);
        var youngerEntry = await bankAccountEntryRepository.GetNextYounger(accountId, endDate);

        BankAccountDto bankAccountDto = new()
        {
            AccountId = account.AccountId,
            UserId = account.UserId,
            Name = account.Name,
            AccountLabel = account.AccountType,
            NextOlderEntry = olderEntry is null ? null : olderEntry.ToDto(),
            NextYoungerEntry = youngerEntry is null ? null : youngerEntry.ToDto(),

            Entries = entries.Select(x => new BankAccountEntryDto
            {
                AccountId = x.AccountId,
                EntryId = x.EntryId,
                PostingDate = x.PostingDate,
                Value = x.Value,
                ValueChange = x.ValueChange,
                Description = x.Description,
                Labels = x.Labels.Select(x => new FinancialLabel() { Name = x.Name, Id = x.Id }).ToList()
            })
        };
        return await Task.FromResult(Ok(bankAccountDto));
    }

    [HttpGet("GetEntry")]
    public async Task<IActionResult> GetEntry([FromQuery] int accountId, [FromQuery] int entryId)
    {
        var userId = ApiAuthenticationHelper.GetUserId(User);
        if (userId is null) return BadRequest();

        var account = await bankAccountEntryRepository.Get(accountId, entryId);
        if (account == null) return NoContent();

        return await Task.FromResult(Ok(account));
    }

    [HttpGet("GetYoungestEntryDate/{accountId:int}")]
    public async Task<IActionResult> GetYoungestEntryDate(int accountId)
    {
        var userId = ApiAuthenticationHelper.GetUserId(User);
        if (userId is null) return BadRequest();

        var account = await bankAccountRepository.Get(accountId);

        if (account == null) return NoContent();
        if (account.UserId != userId) return BadRequest();

        var entry = await bankAccountEntryRepository.GetYoungest(accountId);
        if (entry is not null)
            return await Task.FromResult(Ok(entry.PostingDate));

        return await Task.FromResult(NoContent());
    }

    [HttpGet("GetOldestEntryDate/{accountId:int}")]
    public async Task<IActionResult> GetOldestEntryDate(int accountId)
    {
        var userId = ApiAuthenticationHelper.GetUserId(User);
        if (userId is null) return BadRequest();

        var account = await bankAccountRepository.Get(accountId);

        if (account == null) return NoContent();
        if (account.UserId != userId) return BadRequest();

        var entry = await bankAccountEntryRepository.GetOldest(accountId);
        if (entry is not null)
            return await Task.FromResult(Ok(entry.PostingDate));

        return await Task.FromResult(NoContent());
    }

    [HttpPost("Add")]
    public async Task<IActionResult> Add(AddAccount addAccount)
    {
        var userId = ApiAuthenticationHelper.GetUserId(User);
        if (!userId.HasValue) return BadRequest();
        if (!await userPlanVerifier.CanAddMoreAccounts(userId.Value))
            return BadRequest("Too many accounts. In order to add this account upgrade to higher tier or delete existing one.");

        return Ok(await bankAccountRepository.Add(userId.Value, addAccount.accountName));
    }

    [HttpPost("AddEntry")]
    public async Task<IActionResult> AddEntry(AddBankAccountEntry addEntry)
    {
        var userId = ApiAuthenticationHelper.GetUserId(User);
        if (!userId.HasValue) return BadRequest();

        if (!await userPlanVerifier.CanAddMoreEntries(userId.Value))
            return BadRequest("Too many entries. In order to add this entry upgrade to higher tier or delete existing one.");

        try
        {
            return Ok(await bankAccountEntryRepository.Add(new BankAccountEntry(addEntry.AccountId, addEntry.EntryId, addEntry.PostingDate, addEntry.Value, addEntry.ValueChange)
            {
                Description = addEntry.Description,
            }));
        }
        catch (Exception ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpPut("Update")]
    public async Task<IActionResult> Update(UpdateAccount updateAccount)
    {
        var userId = ApiAuthenticationHelper.GetUserId(User);
        if (userId is null) return BadRequest();

        var account = await bankAccountRepository.Get(updateAccount.accountId);

        if (account == null || account.UserId != userId) return BadRequest();

        if (updateAccount.accountType is null)
            return Ok(await bankAccountRepository.Update(updateAccount.accountId, updateAccount.accountName));
        else
            return Ok(await bankAccountRepository.Update(updateAccount.accountId, updateAccount.accountName, updateAccount.accountType.Value));
    }

    [HttpDelete("Delete/{accountId:int}")]
    public async Task<IActionResult> Delete(int accountId)
    {
        var userId = ApiAuthenticationHelper.GetUserId(User);
        if (userId is null) return BadRequest();

        var account = await bankAccountRepository.Get(accountId);
        if (account == null || account.UserId != userId) return BadRequest();

        await bankAccountEntryRepository.Delete(accountId);
        return Ok(await bankAccountRepository.Delete(accountId));
    }

    [HttpDelete("DeleteEntry/{accountId:int}/{entryId:int}")]
    public async Task<IActionResult> DeleteEntry(int accountId, int entryId)
    {
        var userId = ApiAuthenticationHelper.GetUserId(User);
        if (userId is null) return BadRequest();

        var account = await bankAccountRepository.Get(accountId);
        if (account == null || account.UserId != userId) return BadRequest();

        return Ok(await bankAccountEntryRepository.Delete(accountId, entryId));
    }

    [HttpPut("UpdateEntry")]
    public async Task<IActionResult> UpdateEntry(UpdateBankAccountEntry updateEntry)
    {
        var userId = ApiAuthenticationHelper.GetUserId(User);
        if (userId is null) return BadRequest();

        var account = await bankAccountRepository.Get(updateEntry.AccountId);
        if (account == null || account.UserId != userId) return BadRequest();

        var newEntry = new BankAccountEntry(updateEntry.AccountId, updateEntry.EntryId, updateEntry.PostingDate, updateEntry.Value,
            updateEntry.ValueChange)
        {
            Description = updateEntry.Description,
        };

        if (updateEntry.Labels is null)
            newEntry.Labels = new List<FinancialLabel>();
        else
            newEntry.Labels = updateEntry.Labels.Select(x => new FinancialLabel() { Name = x.Name, Id = x.Id }).ToList();

        return Ok(await bankAccountEntryRepository.Update(newEntry));
    }
}
