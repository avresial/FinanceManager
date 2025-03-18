using FinanceManager.Api.Helpers;
using FinanceManager.Application.Commands.Account;
using FinanceManager.Domain.Entities.Accounts;
using FinanceManager.Domain.Entities.Accounts.Entries;
using FinanceManager.Domain.Repositories.Account;
using Microsoft.AspNetCore.Mvc;

namespace FinanceManager.Api.Controllers.Accounts
{
    [Route("api/[controller]")]
    [ApiController]
    public class StockAccountController : ControllerBase
    {
        private readonly IAccountRepository<StockAccount> stockAccountRepository;
        private readonly IAccountEntryRepository<StockAccountEntry> stockAccountEntryRepository;

        public StockAccountController(IAccountRepository<StockAccount> stockAccountRepository,
            IAccountEntryRepository<StockAccountEntry> stockAccountEntryRepository)
        {
            this.stockAccountRepository = stockAccountRepository ?? throw new ArgumentNullException(nameof(stockAccountRepository));
            this.stockAccountEntryRepository = stockAccountEntryRepository ?? throw new ArgumentNullException(nameof(stockAccountEntryRepository));
        }

        [HttpGet]
        public async Task<IActionResult> GetAllAccounts()
        {
            var userId = ApiAuthenticationHelper.GetUserId(User);
            if (userId is null) return BadRequest("User ID is null.");

            var accounts = await Task.FromResult(stockAccountRepository.GetAvailableAccounts(userId.Value));
            if (accounts == null) return NoContent();

            return Ok(accounts);
        }

        [HttpGet("{accountId:int}")]
        public async Task<IActionResult> GetAccount(int accountId)
        {
            var userId = ApiAuthenticationHelper.GetUserId(User);
            if (userId is null) return BadRequest("User ID is null.");

            var account = await Task.FromResult(stockAccountRepository.Get(accountId));
            if (account == null) return NoContent();
            if (account.UserId != userId) return BadRequest("User ID does not match the account owner.");

            return Ok(account);
        }

        [HttpGet("{accountId:int}/entries")]
        public async Task<IActionResult> GetAccountEntries(int accountId, [FromQuery] DateTime startDate, [FromQuery] DateTime endDate)
        {
            var userId = ApiAuthenticationHelper.GetUserId(User);
            if (userId is null) return BadRequest("User ID is null.");

            var account = await Task.FromResult(stockAccountRepository.Get(accountId));
            if (account == null) return NoContent();
            if (account.UserId != userId) return BadRequest("User ID does not match the account owner.");

            var entries = await Task.FromResult(stockAccountEntryRepository.Get(accountId, startDate, endDate));
            account.Add(entries, false);

            return Ok(account);
        }

        [HttpPost]
        public async Task<IActionResult> AddAccount(AddAccount addAccount)
        {
            var userId = ApiAuthenticationHelper.GetUserId(User);
            if (!userId.HasValue) return BadRequest("User ID is null.");

            var result = await Task.FromResult(stockAccountRepository.Add(userId.Value, addAccount.accountName));
            return Ok(result);
        }

        [HttpPost("entries")]
        public async Task<IActionResult> AddAccountEntry(AddStockAccountEntry addEntry)
        {
            var userId = ApiAuthenticationHelper.GetUserId(User);
            if (!userId.HasValue) return BadRequest("User ID is null.");

            var result = await Task.FromResult(stockAccountEntryRepository.Add(addEntry.entry));
            return Ok(result);
        }

        [HttpPut]
        public async Task<IActionResult> UpdateAccount(UpdateAccount updateAccount)
        {
            var userId = ApiAuthenticationHelper.GetUserId(User);
            if (userId is null) return BadRequest("User ID is null.");

            var account = await Task.FromResult(stockAccountRepository.Get(updateAccount.accountId));
            if (account == null || account.UserId != userId) return BadRequest("User ID does not match the account owner.");

            var result = await Task.FromResult(stockAccountRepository.Update(updateAccount.accountId, updateAccount.accountName));
            return Ok(result);
        }

        [HttpPut("entries")]
        public async Task<IActionResult> UpdateAccountEntry(StockAccountEntry entry)
        {
            var userId = ApiAuthenticationHelper.GetUserId(User);
            if (userId is null) return BadRequest("User ID is null.");

            var account = await Task.FromResult(stockAccountRepository.Get(entry.AccountId));
            if (account == null || account.UserId != userId) return BadRequest("User ID does not match the account owner.");

            var result = await Task.FromResult(stockAccountEntryRepository.Update(entry));
            return Ok(result);
        }

        [HttpDelete]
        public async Task<IActionResult> DeleteAccount(DeleteAccount deleteAccount)
        {
            var userId = ApiAuthenticationHelper.GetUserId(User);
            if (userId is null) return BadRequest("User ID is null.");

            var account = await Task.FromResult(stockAccountRepository.Get(deleteAccount.accountId));
            if (account == null || account.UserId != userId) return BadRequest("User ID does not match the account owner.");

            var result = await Task.FromResult(stockAccountRepository.Delete(deleteAccount.accountId));
            return Ok(result);
        }

        [HttpDelete("entries/{accountId:int}/{entryId:int}")]
        public async Task<IActionResult> DeleteAccountEntry(int accountId, int entryId)
        {
            var userId = ApiAuthenticationHelper.GetUserId(User);
            if (userId is null) return BadRequest("User ID is null.");

            var account = await Task.FromResult(stockAccountRepository.Get(accountId));
            if (account == null || account.UserId != userId) return BadRequest("User ID does not match the account owner.");

            var result = await Task.FromResult(stockAccountEntryRepository.Delete(accountId, entryId));
            return Ok(result);
        }
    }
}
