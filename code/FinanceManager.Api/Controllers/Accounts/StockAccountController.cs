using FinanceManager.Api.Helpers;
using FinanceManager.Application.Commands.Account;
using FinanceManager.Domain.Entities.Accounts;
using FinanceManager.Domain.Entities.Accounts.Entries;
using FinanceManager.Domain.Repositories.Account;
using FinanceManager.Infrastructure.Dtos;
using FinanceManager.Infrastructure.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinanceManager.Api.Controllers.Accounts
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class StockAccountController : ControllerBase
    {
        private readonly IAccountRepository<StockAccount> _accountRepository;
        private readonly IStockAccountEntryRepository<StockAccountEntry> _entryRepository;

        public StockAccountController(IAccountRepository<StockAccount> stockAccountRepository,
            IStockAccountEntryRepository<StockAccountEntry> stockAccountEntryRepository)
        {
            this._accountRepository = stockAccountRepository ?? throw new ArgumentNullException(nameof(stockAccountRepository));
            this._entryRepository = stockAccountEntryRepository ?? throw new ArgumentNullException(nameof(stockAccountEntryRepository));
        }

        [HttpGet]
        public async Task<IActionResult> Get()
        {
            var userId = ApiAuthenticationHelper.GetUserId(User);
            if (userId is null) return BadRequest("User ID is null.");

            var accounts = await _accountRepository.GetAvailableAccounts(userId.Value);
            if (accounts == null) return NoContent();

            return Ok(accounts);
        }

        [HttpGet("{accountId:int}")]
        public async Task<IActionResult> Get(int accountId)
        {
            var userId = ApiAuthenticationHelper.GetUserId(User);
            if (userId is null) return BadRequest("User ID is null.");

            var account = await _accountRepository.Get(accountId);
            if (account == null) return NoContent();
            if (account.UserId != userId) return StatusCode(StatusCodes.Status403Forbidden, "Forbidden: User does not own this account.");
            return Ok(account);
        }

        [HttpGet("{accountId:int}&{startDate:DateTime}&{endDate:DateTime}")]
        public async Task<IActionResult> Get(int accountId, DateTime startDate, DateTime endDate)
        {
            var userId = ApiAuthenticationHelper.GetUserId(User);
            if (userId is null) return BadRequest("User ID is null.");

            var account = await _accountRepository.Get(accountId);
            if (account == null) return NoContent();
            if (account.UserId != userId) return BadRequest("User ID does not match the account owner.");

            var entries = await _entryRepository.Get(accountId, startDate, endDate);

            StockAccountDto bankAccountDto = new()
            {
                AccountId = account.AccountId,
                UserId = account.UserId,
                Name = account.Name,
                NextOlderEntries = (await _entryRepository.GetNextOlder(accountId, startDate)).ToDictionary(x => x.Key, x => x.Value.ToDto()),
                NextYoungerEntries = (await _entryRepository.GetNextYounger(accountId, startDate)).ToDictionary(x => x.Key, x => x.Value.ToDto()),
                Entries = entries.Select(x => new StockAccountEntryDto
                {
                    Ticker = x.Ticker,
                    InvestmentType = x.InvestmentType,
                    AccountId = x.AccountId,
                    EntryId = x.EntryId,
                    PostingDate = x.PostingDate,
                    Value = x.Value,
                    ValueChange = x.ValueChange,
                })
            };

            return Ok(bankAccountDto);
        }

        [HttpGet("GetYoungestEntryDate/{accountId:int}")]
        public async Task<IActionResult> GetYoungestEntryDate(int accountId)
        {
            var userId = ApiAuthenticationHelper.GetUserId(User);
            if (userId is null) return BadRequest();

            var account = await _accountRepository.Get(accountId);

            if (account == null) return NoContent();
            if (account.UserId != userId) return BadRequest();

            var entry = await _entryRepository.GetYoungest(accountId);
            if (entry is not null)
                return await Task.FromResult(Ok(entry.PostingDate));

            return await Task.FromResult(NoContent());
        }

        [HttpGet("GetOldestEntryDate/{accountId:int}")]
        public async Task<IActionResult> GetOldestEntryDate(int accountId)
        {
            var userId = ApiAuthenticationHelper.GetUserId(User);
            if (userId is null) return BadRequest();

            var account = await _accountRepository.Get(accountId);

            if (account == null) return NoContent();
            if (account.UserId != userId) return BadRequest();

            var entry = await _entryRepository.GetOldest(accountId);
            if (entry is not null)
                return Ok(entry.PostingDate);

            return await Task.FromResult(NoContent());
        }

        [HttpPost("Add")]
        public async Task<IActionResult> Add(AddAccount addAccount)
        {
            var userId = ApiAuthenticationHelper.GetUserId(User);
            if (!userId.HasValue) return BadRequest("User ID is null.");

            return Ok(await _accountRepository.Add(userId.Value, addAccount.accountName));
        }

        [HttpPost("AddEntry")]
        public async Task<IActionResult> AddEntry(AddStockAccountEntry addEntry)
        {
            var userId = ApiAuthenticationHelper.GetUserId(User);
            if (!userId.HasValue) return BadRequest("User ID is null.");

            var result = await _entryRepository.Add(addEntry.entry);
            return Ok(result);
        }

        [HttpPut("Update")]
        public async Task<IActionResult> Update(UpdateAccount updateAccount)
        {
            var userId = ApiAuthenticationHelper.GetUserId(User);
            if (userId is null) return BadRequest("User ID is null.");

            var account = await _accountRepository.Get(updateAccount.accountId);
            if (account == null || account.UserId != userId) return BadRequest("User ID does not match the account owner.");

            var result = await _accountRepository.Update(updateAccount.accountId, updateAccount.accountName);
            return Ok(result);
        }


        [HttpDelete("Delete/{accountId:int}")]
        public async Task<IActionResult> Delete(int accountId)
        {
            var userId = ApiAuthenticationHelper.GetUserId(User);
            if (userId is null) return BadRequest("User ID is null.");

            var account = await _accountRepository.Get(accountId);
            if (account == null) return NotFound("Account not found.");
            if (account.UserId != userId) return StatusCode(StatusCodes.Status403Forbidden, "Forbidden: User does not own this account.");

            await _entryRepository.Delete(accountId);
            return Ok(await _accountRepository.Delete(accountId));
        }

        [HttpDelete("DeleteEntry/{accountId:int}/{entryId:int}")]
        public async Task<IActionResult> DeleteEntry(int accountId, int entryId)
        {
            var userId = ApiAuthenticationHelper.GetUserId(User);
            if (userId is null) return BadRequest("User ID is null.");

            var account = await _accountRepository.Get(accountId);
            if (account == null || account.UserId != userId) return BadRequest("User ID does not match the account owner.");

            var result = await _entryRepository.Delete(accountId, entryId);
            return Ok(result);
        }

        [HttpPut("UpdateEntry")]
        public async Task<IActionResult> UpdateEntry(UpdateStockAccountEntry updateCommand)
        {
            var userId = ApiAuthenticationHelper.GetUserId(User);
            if (userId is null) return BadRequest("User ID is null.");

            var account = await _accountRepository.Get(updateCommand.AccountId);
            if (account == null || account.UserId != userId) return BadRequest("User ID does not match the account owner.");

            var entryToUpdate = await _entryRepository.Get(updateCommand.AccountId, updateCommand.EntryId);

            if (entryToUpdate is null) return NoContent();

            entryToUpdate.Update(new StockAccountEntry(updateCommand.AccountId, updateCommand.EntryId, updateCommand.PostingDate, updateCommand.Value,
                updateCommand.ValueChange, updateCommand.Ticker, updateCommand.investmentType));

            var result = await _entryRepository.Update(entryToUpdate);
            return Ok(result);
        }
    }
}
