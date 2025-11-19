using FinanceManager.Api.Helpers;
using FinanceManager.Application.Commands.Account;
using FinanceManager.Application.Services;
using FinanceManager.Domain.Entities.Accounts;
using FinanceManager.Domain.Entities.Accounts.Entries;
using FinanceManager.Domain.Entities.Imports;
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
    IAccountEntryRepository<BankAccountEntry> bankAccountEntryRepository, IUserPlanVerifier userPlanVerifier, IBankAccountImportService importService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var userId = ApiAuthenticationHelper.GetUserId(User);
        var accounts = await bankAccountRepository.GetAvailableAccounts(userId).ToListAsync();

        return accounts.Count == 0 ? NotFound() : Ok(accounts);
    }

    [HttpGet("{accountId:int}")]
    public async Task<IActionResult> Get(int accountId)
    {
        var account = await bankAccountRepository.Get(accountId);
        if (account == null) return NotFound();
        if (account.UserId != ApiAuthenticationHelper.GetUserId(User)) return Forbid();

        return Ok(account);
    }

    [HttpGet("{accountId:int}&{startDate:DateTime}&{endDate:DateTime}")]
    public async Task<IActionResult> Get(int accountId, DateTime startDate, DateTime endDate)
    {
        var account = await bankAccountRepository.Get(accountId);

        if (account == null) return NotFound();
        if (account.UserId != ApiAuthenticationHelper.GetUserId(User)) return Forbid();

        var entries = bankAccountEntryRepository.Get(accountId, startDate, endDate);
        var olderEntry = await bankAccountEntryRepository.GetNextOlder(accountId, startDate);
        var youngerEntry = await bankAccountEntryRepository.GetNextYounger(accountId, endDate);

        return Ok(account.ToDto(olderEntry, youngerEntry, await entries.ToListAsync()));
    }

    [HttpGet("GetEntry")]
    public async Task<IActionResult> GetEntry([FromQuery] int accountId, [FromQuery] int entryId)
    {
        var account = await bankAccountEntryRepository.Get(accountId, entryId);
        if (account == null) return NotFound();

        return Ok(account);
    }

    [HttpGet("GetYoungestEntryDate/{accountId:int}")]
    public async Task<IActionResult> GetYoungestEntryDate(int accountId)
    {
        var account = await bankAccountRepository.Get(accountId);

        if (account == null) return NotFound();
        if (account.UserId != ApiAuthenticationHelper.GetUserId(User)) return BadRequest();

        var entry = await bankAccountEntryRepository.GetYoungest(accountId);
        if (entry is null) return NotFound();

        return Ok(entry.PostingDate);
    }

    [HttpGet("GetOldestEntryDate/{accountId:int}")]
    public async Task<IActionResult> GetOldestEntryDate(int accountId)
    {
        var account = await bankAccountRepository.Get(accountId);

        if (account == null) return NotFound();
        if (account.UserId != ApiAuthenticationHelper.GetUserId(User)) return BadRequest();

        var entry = await bankAccountEntryRepository.GetOldest(accountId);

        return entry is null ? NoContent() : Ok(entry.PostingDate);
    }

    [HttpPost("Add")]
    public async Task<IActionResult> Add(AddAccount addAccount)
    {
        var userId = ApiAuthenticationHelper.GetUserId(User);

        if (!await userPlanVerifier.CanAddMoreAccounts(userId))
            return BadRequest("Too many accounts. In order to add this account upgrade to higher tier or delete existing one.");

        return Ok(await bankAccountRepository.Add(userId, addAccount.accountName));
    }

    [HttpPost("AddEntry")]
    public async Task<IActionResult> AddEntry(AddBankAccountEntry addEntry)
    {
        if (!await userPlanVerifier.CanAddMoreEntries(ApiAuthenticationHelper.GetUserId(User)))
            return BadRequest("Too many entries. In order to add this entry upgrade to higher tier or delete existing one.");

        return Ok(await bankAccountEntryRepository.Add(new BankAccountEntry(addEntry.AccountId, addEntry.EntryId, addEntry.PostingDate, addEntry.Value, addEntry.ValueChange)
        {
            Description = addEntry.Description,
        }));
    }

    [HttpPut("Update")]
    public async Task<IActionResult> Update(UpdateAccount updateAccount)
    {
        var account = await bankAccountRepository.Get(updateAccount.accountId);

        if (account == null || account.UserId != ApiAuthenticationHelper.GetUserId(User)) return BadRequest();

        if (updateAccount.accountType is null)
            return Ok(await bankAccountRepository.Update(updateAccount.accountId, updateAccount.accountName));

        return Ok(await bankAccountRepository.Update(updateAccount.accountId, updateAccount.accountName, updateAccount.accountType.Value));
    }

    [HttpDelete("Delete/{accountId:int}")]
    public async Task<IActionResult> Delete(int accountId)
    {
        var account = await bankAccountRepository.Get(accountId);
        if (account is null || account.UserId != ApiAuthenticationHelper.GetUserId(User)) return BadRequest();

        await bankAccountEntryRepository.Delete(accountId);
        return Ok(await bankAccountRepository.Delete(accountId));
    }

    [HttpDelete("DeleteEntry/{accountId:int}/{entryId:int}")]
    public async Task<IActionResult> DeleteEntry(int accountId, int entryId)
    {
        var account = await bankAccountRepository.Get(accountId);
        if (account is null || account.UserId != ApiAuthenticationHelper.GetUserId(User)) return BadRequest();

        return Ok(await bankAccountEntryRepository.Delete(accountId, entryId));
    }

    [HttpPut("UpdateEntry")]
    public async Task<IActionResult> UpdateEntry(UpdateBankAccountEntry updateEntry)
    {
        var account = await bankAccountRepository.Get(updateEntry.AccountId);
        if (account == null || account.UserId != ApiAuthenticationHelper.GetUserId(User)) return BadRequest();

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

    [HttpPost("ImportBankEntries")]
    public async Task<IActionResult> ImportBankEntries([FromBody] BankDataImportDto importDto)
    {
        if (importDto is null) return BadRequest("No import data provided.");

        var domainEntries = importDto.Entries.Select(e => new BankEntryImport(e.PostingDate, e.ValueChange));
        var domainResult = await importService.ImportEntries(ApiAuthenticationHelper.GetUserId(User), importDto.AccountId, domainEntries);

        return Ok(domainResult);
    }

    [HttpPost("ResolveImportConflicts")]
    public async Task<IActionResult> ResolveImportConflicts([FromBody] IEnumerable<ResolvedImportConflict> resolvedConflicts)
    {
        if (resolvedConflicts is null) return BadRequest("No resolved conflicts provided.");

        foreach (var accountId in resolvedConflicts.Select(rc => rc.AccountId).Distinct())
        {
            var account = await bankAccountRepository.Get(accountId);
            if (account is null || account.UserId != ApiAuthenticationHelper.GetUserId(User))
                return Forbid("Account not found or access denied.");
        }

        await importService.ApplyResolvedConflicts(resolvedConflicts);
        return Ok();
    }
}