using FinanceManager.Api.Helpers;
using FinanceManager.Application.Commands.Account;
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
public class BankAccountController(IAccountRepository<BankAccount> bankAccountRepository, AccountIdProvider accountIdProvider,
IAccountEntryRepository<BankAccountEntry> bankAccountEntryRepository) : ControllerBase
{
    private readonly AccountIdProvider accountIdProvider = accountIdProvider;
    private readonly IAccountRepository<BankAccount> bankAccountRepository = bankAccountRepository;
    private readonly IAccountEntryRepository<BankAccountEntry> bankAccountEntryRepository = bankAccountEntryRepository;

    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var userId = ApiAuthenticationHelper.GetUserId(User);
        if (userId is null) return BadRequest();

        var account = bankAccountRepository.GetAvailableAccounts(userId.Value);

        if (account == null) return NoContent();

        return await Task.FromResult(Ok(account));
    }

    [HttpGet("{accountId:int}")]
    public async Task<IActionResult> Get(int accountId)
    {
        var userId = ApiAuthenticationHelper.GetUserId(User);
        if (userId is null) return BadRequest();

        var account = bankAccountRepository.Get(accountId);
        if (account == null) return NoContent();
        if (account.UserId != userId) return BadRequest();

        return await Task.FromResult(Ok(account));
    }

    [HttpGet("{accountId:int}&{startDate:DateTime}&{endDate:DateTime}")]
    public async Task<IActionResult> Get(int accountId, DateTime startDate, DateTime endDate)
    {
        var userId = ApiAuthenticationHelper.GetUserId(User);
        if (userId is null) return BadRequest();

        var account = bankAccountRepository.Get(accountId);

        if (account == null) return NoContent();
        if (account.UserId != userId) return BadRequest();

        IEnumerable<BankAccountEntry> entries = bankAccountEntryRepository.Get(accountId, startDate, endDate);
        BankAccountDto bankAccountDto = new()
        {
            AccountId = account.AccountId,
            UserId = account.UserId,
            Name = account.Name,
            AccountType = account.AccountType,
            OlderThenLoadedEntry = account.OlderThenLoadedEntry,
            YoungerThenLoadedEntry = account.YoungerThenLoadedEntry,
            Entries = entries.Select(x => new BankAccountEntryDto
            {
                AccountId = x.AccountId,
                EntryId = x.EntryId,
                PostingDate = x.PostingDate,
                Value = x.Value,
                ValueChange = x.ValueChange,
            })
        };
        return await Task.FromResult(Ok(bankAccountDto));
    }

    [HttpGet("GetYoungestEntryDate/{accountId:int}")]
    public async Task<IActionResult> GetYoungestEntryDate(int accountId)
    {
        var userId = ApiAuthenticationHelper.GetUserId(User);
        if (userId is null) return BadRequest();

        var account = bankAccountRepository.Get(accountId);

        if (account == null) return NoContent();
        if (account.UserId != userId) return BadRequest();

        var entry = bankAccountEntryRepository.GetYoungest(accountId);
        if (entry is not null)
            return await Task.FromResult(Ok(entry.PostingDate));

        return await Task.FromResult(NoContent());
    }

    [HttpGet("GetOldestEntryDate/{accountId:int}")]
    public async Task<IActionResult> GetOldestEntryDate(int accountId)
    {
        var userId = ApiAuthenticationHelper.GetUserId(User);
        if (userId is null) return BadRequest();

        var account = bankAccountRepository.Get(accountId);

        if (account == null) return NoContent();
        if (account.UserId != userId) return BadRequest();

        var entry = bankAccountEntryRepository.GetOldest(accountId);
        if (entry is not null)
            return await Task.FromResult(Ok(entry.PostingDate));

        return await Task.FromResult(NoContent());
    }

    [HttpPost("Add")]
    public async Task<IActionResult> Add(AddAccount addAccount)
    {
        var userId = ApiAuthenticationHelper.GetUserId(User);
        if (!userId.HasValue) return BadRequest();

        var id = accountIdProvider.GetMaxId(userId.Value);
        int newId = id is null ? 1 : id.Value + 1;
        return await Task.FromResult(Ok(bankAccountRepository.Add(newId, userId.Value, addAccount.accountName)));
    }

    [HttpPost("AddEntry")]
    public async Task<IActionResult> AddEntry(AddBankAccountEntry addEntry)
    {
        var userId = ApiAuthenticationHelper.GetUserId(User);
        if (!userId.HasValue) return BadRequest();

        return await Task.FromResult(Ok(bankAccountEntryRepository.Add(addEntry.entry)));
    }

    [HttpPut("Update")]
    public async Task<IActionResult> Update(UpdateAccount updateAccount)
    {
        var userId = ApiAuthenticationHelper.GetUserId(User);
        if (userId is null) return BadRequest();

        var account = bankAccountRepository.Get(updateAccount.accountId);

        if (account == null || account.UserId != userId) return BadRequest();
        return await Task.FromResult(Ok(bankAccountRepository.Update(updateAccount.accountId, updateAccount.accountName)));
    }

    [HttpDelete("Delete")]
    public async Task<IActionResult> Delete(DeleteAccount deleteAccount)
    {
        var userId = ApiAuthenticationHelper.GetUserId(User);
        if (userId is null) return BadRequest();

        var account = bankAccountRepository.Get(deleteAccount.accountId);

        if (account == null || account.UserId != userId) return BadRequest();
        return await Task.FromResult(Ok(bankAccountRepository.Delete(deleteAccount.accountId)));
    }

    [HttpDelete("DeleteEntry/{accountId:int}/{entryId:int}")]
    public async Task<IActionResult> DeleteEntry(int accountId, int entryId)
    {
        var userId = ApiAuthenticationHelper.GetUserId(User);
        if (userId is null) return BadRequest();

        var account = bankAccountRepository.Get(accountId);
        if (account == null || account.UserId != userId) return BadRequest();

        return await Task.FromResult(Ok(bankAccountEntryRepository.Delete(accountId, entryId)));
    }

    [HttpPut("UpdateEntry")]
    public async Task<IActionResult> UpdateEntry(BankAccountEntry entry)
    {
        var userId = ApiAuthenticationHelper.GetUserId(User);
        if (userId is null) return BadRequest();

        var account = bankAccountRepository.Get(entry.AccountId);
        if (account == null || account.UserId != userId) return BadRequest();

        return await Task.FromResult(Ok(bankAccountEntryRepository.Update(entry)));
    }
}
