using FinanceManager.Api.Helpers;
using FinanceManager.Application.Commands.Account;
using FinanceManager.Application.Services;
using FinanceManager.Domain.Entities.Accounts;
using FinanceManager.Domain.Entities.Accounts.Entries;
using FinanceManager.Domain.Providers;
using FinanceManager.Domain.Repositories.Account;
using FinanceManager.Infrastructure.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinanceManager.Api.Controllers.Accounts;

[Authorize]
[Route("api/[controller]")]
[ApiController]
public class BankAccountController(IBankAccountRepository<BankAccount> bankAccountRepository, AccountIdProvider accountIdProvider,
IAccountEntryRepository<BankAccountEntry> bankAccountEntryRepository, UserPlanVerifier userPlanVerifier) : ControllerBase
{
    private readonly AccountIdProvider _accountIdProvider = accountIdProvider;
    private readonly IBankAccountRepository<BankAccount> _bankAccountRepository = bankAccountRepository;
    private readonly IAccountEntryRepository<BankAccountEntry> _bankAccountEntryRepository = bankAccountEntryRepository;
    private readonly UserPlanVerifier _userPlanVerifier = userPlanVerifier;

    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var userId = ApiAuthenticationHelper.GetUserId(User);
        if (userId is null) return BadRequest();

        var account = await _bankAccountRepository.GetAvailableAccounts(userId.Value);

        if (account == null) return NoContent();

        return await Task.FromResult(Ok(account));
    }

    [HttpGet("{accountId:int}")]
    public async Task<IActionResult> Get(int accountId)
    {
        var userId = ApiAuthenticationHelper.GetUserId(User);
        if (userId is null) return BadRequest();

        var account = await _bankAccountRepository.Get(accountId);
        if (account == null) return NoContent();
        if (account.UserId != userId) return BadRequest();

        return await Task.FromResult(Ok(account));
    }

    [HttpGet("{accountId:int}&{startDate:DateTime}&{endDate:DateTime}")]
    public async Task<IActionResult> Get(int accountId, DateTime startDate, DateTime endDate)
    {
        var userId = ApiAuthenticationHelper.GetUserId(User);
        if (userId is null) return BadRequest();

        var account = await _bankAccountRepository.Get(accountId);

        if (account == null) return NoContent();
        if (account.UserId != userId) return BadRequest();

        IEnumerable<BankAccountEntry> entries = await _bankAccountEntryRepository.Get(accountId, startDate, endDate);

        DateTime? olderThanLoadedEntryDate = null;
        DateTime? youngerThanLoadedEntryDate = null;

        var olderEntry = await _bankAccountEntryRepository.GetNextOlder(accountId, startDate);
        if (olderEntry is not null) olderThanLoadedEntryDate = olderEntry.PostingDate;

        var youngerEntry = await _bankAccountEntryRepository.GetNextYounger(accountId, endDate);
        if (youngerEntry is not null) youngerThanLoadedEntryDate = youngerEntry.PostingDate;

        BankAccountDto bankAccountDto = new()
        {
            AccountId = account.AccountId,
            UserId = account.UserId,
            Name = account.Name,
            AccountLabel = account.AccountType,
            OlderThanLoadedEntry = olderThanLoadedEntryDate,
            YoungerThanLoadedEntry = youngerThanLoadedEntryDate,
            Entries = entries.Select(x => new BankAccountEntryDto
            {
                AccountId = x.AccountId,
                EntryId = x.EntryId,
                PostingDate = x.PostingDate,
                Value = x.Value,
                ValueChange = x.ValueChange,
                ExpenseType = x.ExpenseType,
                Description = x.Description
            })
        };
        return await Task.FromResult(Ok(bankAccountDto));
    }

    [HttpGet("GetEntry")]
    public async Task<IActionResult> GetEntry([FromQuery] int accountId, [FromQuery] int entryId)
    {
        var userId = ApiAuthenticationHelper.GetUserId(User);
        if (userId is null) return BadRequest();

        var account = await _bankAccountEntryRepository.Get(accountId, entryId);
        if (account == null) return NoContent();

        return await Task.FromResult(Ok(account));
    }

    [HttpGet("GetYoungestEntryDate/{accountId:int}")]
    public async Task<IActionResult> GetYoungestEntryDate(int accountId)
    {
        var userId = ApiAuthenticationHelper.GetUserId(User);
        if (userId is null) return BadRequest();

        var account = await _bankAccountRepository.Get(accountId);

        if (account == null) return NoContent();
        if (account.UserId != userId) return BadRequest();

        var entry = await _bankAccountEntryRepository.GetYoungest(accountId);
        if (entry is not null)
            return await Task.FromResult(Ok(entry.PostingDate));

        return await Task.FromResult(NoContent());
    }

    [HttpGet("GetOldestEntryDate/{accountId:int}")]
    public async Task<IActionResult> GetOldestEntryDate(int accountId)
    {
        var userId = ApiAuthenticationHelper.GetUserId(User);
        if (userId is null) return BadRequest();

        var account = await _bankAccountRepository.Get(accountId);

        if (account == null) return NoContent();
        if (account.UserId != userId) return BadRequest();

        var entry = await _bankAccountEntryRepository.GetOldest(accountId);
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

        var id = await _accountIdProvider.GetMaxId();
        int newId = id is null ? 1 : id.Value + 1;
        return Ok(await _bankAccountRepository.Add(userId.Value, newId, addAccount.accountName));
    }

    [HttpPost("AddEntry")]
    public async Task<IActionResult> AddEntry(AddBankAccountEntry addEntry)
    {
        var userId = ApiAuthenticationHelper.GetUserId(User);
        if (!userId.HasValue) return BadRequest();

        if (!await _userPlanVerifier.CanAddMoreEntries(userId.Value))
            return BadRequest("Too many entries. In order to add this entry upgrade to higher tier or delete existing one.");

        try
        {
            return Ok(await _bankAccountEntryRepository.Add(addEntry.entry));
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

        var account = await _bankAccountRepository.Get(updateAccount.accountId);

        if (account == null || account.UserId != userId) return BadRequest();

        if (updateAccount.accountType is null)
            return Ok(await _bankAccountRepository.Update(updateAccount.accountId, updateAccount.accountName));
        else
            return Ok(await _bankAccountRepository.Update(updateAccount.accountId, updateAccount.accountName, updateAccount.accountType.Value));
    }

    [HttpDelete("Delete/{accountId:int}")]
    public async Task<IActionResult> Delete(int accountId)
    {
        var userId = ApiAuthenticationHelper.GetUserId(User);
        if (userId is null) return BadRequest();

        var account = await _bankAccountRepository.Get(accountId);
        if (account == null || account.UserId != userId) return BadRequest();

        await _bankAccountEntryRepository.Delete(accountId);
        return Ok(await _bankAccountRepository.Delete(accountId));
    }

    [HttpDelete("DeleteEntry/{accountId:int}/{entryId:int}")]
    public async Task<IActionResult> DeleteEntry(int accountId, int entryId)
    {
        var userId = ApiAuthenticationHelper.GetUserId(User);
        if (userId is null) return BadRequest();

        var account = await _bankAccountRepository.Get(accountId);
        if (account == null || account.UserId != userId) return BadRequest();

        return Ok(await _bankAccountEntryRepository.Delete(accountId, entryId));
    }

    [HttpPut("UpdateEntry")]
    public async Task<IActionResult> UpdateEntry(BankAccountEntry entry)
    {
        var userId = ApiAuthenticationHelper.GetUserId(User);
        if (userId is null) return BadRequest();

        var account = await _bankAccountRepository.Get(entry.AccountId);
        if (account == null || account.UserId != userId) return BadRequest();

        return Ok(await _bankAccountEntryRepository.Update(entry));
    }
}
