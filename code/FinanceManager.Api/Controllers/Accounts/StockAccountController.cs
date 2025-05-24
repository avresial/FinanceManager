using FinanceManager.Api.Helpers;
using FinanceManager.Application.Commands.Account;
using FinanceManager.Domain.Entities.Accounts;
using FinanceManager.Domain.Entities.Accounts.Entries;
using FinanceManager.Domain.Providers;
using FinanceManager.Domain.Repositories.Account;
using FinanceManager.Infrastructure.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinanceManager.Api.Controllers.Accounts
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class StockAccountController : ControllerBase
    {
        private readonly AccountIdProvider accountIdProvider;
        private readonly IAccountRepository<StockAccount> stockAccountRepository;
        private readonly IAccountEntryRepository<StockAccountEntry> stockAccountEntryRepository;

        public StockAccountController(IAccountRepository<StockAccount> stockAccountRepository, AccountIdProvider accountIdProvider,
            IAccountEntryRepository<StockAccountEntry> stockAccountEntryRepository)
        {
            this.stockAccountRepository = stockAccountRepository ?? throw new ArgumentNullException(nameof(stockAccountRepository));
            this.accountIdProvider = accountIdProvider;
            this.stockAccountEntryRepository = stockAccountEntryRepository ?? throw new ArgumentNullException(nameof(stockAccountEntryRepository));
        }

        [HttpGet]
        public async Task<IActionResult> Get()
        {
            var userId = ApiAuthenticationHelper.GetUserId(User);
            if (userId is null) return BadRequest("User ID is null.");

            var accounts = await Task.FromResult(stockAccountRepository.GetAvailableAccounts(userId.Value));
            if (accounts == null) return NoContent();

            return Ok(accounts);
        }

        [HttpGet("{accountId:int}")]
        public async Task<IActionResult> Get(int accountId)
        {
            var userId = ApiAuthenticationHelper.GetUserId(User);
            if (userId is null) return BadRequest("User ID is null.");

            var account = await stockAccountRepository.Get(accountId);
            if (account == null) return NoContent();
            if (account.UserId != userId) return StatusCode(StatusCodes.Status403Forbidden, "Forbidden: User does not own this account.");
            return Ok(account);
        }

        [HttpGet("{accountId:int}&{startDate:DateTime}&{endDate:DateTime}")]
        public async Task<IActionResult> Get(int accountId, DateTime startDate, DateTime endDate)
        {
            var userId = ApiAuthenticationHelper.GetUserId(User);
            if (userId is null) return BadRequest("User ID is null.");

            var account = await stockAccountRepository.Get(accountId);
            if (account == null) return NoContent();
            if (account.UserId != userId) return BadRequest("User ID does not match the account owner.");

            var entries = await Task.FromResult(stockAccountEntryRepository.Get(accountId, startDate, endDate));
            StockAccountDto bankAccountDto = new()
            {
                AccountId = account.AccountId,
                UserId = account.UserId,
                Name = account.Name,
                OlderThenLoadedEntry = account.OlderThanLoadedEntry,
                YoungerThenLoadedEntry = account.YoungerThanLoadedEntry,
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

            var account = await stockAccountRepository.Get(accountId);

            if (account == null) return NoContent();
            if (account.UserId != userId) return BadRequest();

            var entry = stockAccountEntryRepository.GetYoungest(accountId);
            if (entry is not null)
                return await Task.FromResult(Ok(entry.PostingDate));

            return await Task.FromResult(NoContent());
        }

        [HttpGet("GetOldestEntryDate/{accountId:int}")]
        public async Task<IActionResult> GetOldestEntryDate(int accountId)
        {
            var userId = ApiAuthenticationHelper.GetUserId(User);
            if (userId is null) return BadRequest();

            var account = await stockAccountRepository.Get(accountId);

            if (account == null) return NoContent();
            if (account.UserId != userId) return BadRequest();

            var entry = stockAccountEntryRepository.GetOldest(accountId);
            if (entry is not null)
                return await Task.FromResult(Ok(entry.PostingDate));

            return await Task.FromResult(NoContent());
        }

        [HttpPost("Add")]
        public async Task<IActionResult> Add(AddAccount addAccount)
        {
            var userId = ApiAuthenticationHelper.GetUserId(User);
            if (!userId.HasValue) return BadRequest("User ID is null.");

            var id = accountIdProvider.GetMaxId();
            int newId = id is null ? 1 : id.Value + 1;
            var result = await Task.FromResult(await stockAccountRepository.Add(newId, userId.Value, addAccount.accountName));
            return Ok(result);
        }

        [HttpPost("AddEntry")]
        public async Task<IActionResult> AddEntry(AddStockAccountEntry addEntry)
        {
            var userId = ApiAuthenticationHelper.GetUserId(User);
            if (!userId.HasValue) return BadRequest("User ID is null.");

            var result = await Task.FromResult(stockAccountEntryRepository.Add(addEntry.entry));
            return Ok(result);
        }

        [HttpPut("Update")]
        public async Task<IActionResult> Update(UpdateAccount updateAccount)
        {
            var userId = ApiAuthenticationHelper.GetUserId(User);
            if (userId is null) return BadRequest("User ID is null.");

            var account = await stockAccountRepository.Get(updateAccount.accountId);
            if (account == null || account.UserId != userId) return BadRequest("User ID does not match the account owner.");

            var result = await Task.FromResult(await stockAccountRepository.Update(updateAccount.accountId, updateAccount.accountName));
            return Ok(result);
        }

        [HttpDelete("Delete")]
        public async Task<IActionResult> Delete(DeleteAccount deleteAccount)
        {
            var userId = ApiAuthenticationHelper.GetUserId(User);
            if (userId is null) return BadRequest("User ID is null.");

            var account = await stockAccountRepository.Get(deleteAccount.accountId);
            if (account == null || account.UserId != userId) return BadRequest("User ID does not match the account owner.");

            var result = await Task.FromResult(await stockAccountRepository.Delete(deleteAccount.accountId));
            return Ok(result);
        }

        [HttpDelete("DeleteEntry/{accountId:int}/{entryId:int}")]
        public async Task<IActionResult> DeleteEntry(int accountId, int entryId)
        {
            var userId = ApiAuthenticationHelper.GetUserId(User);
            if (userId is null) return BadRequest("User ID is null.");

            var account = await stockAccountRepository.Get(accountId);
            if (account == null || account.UserId != userId) return BadRequest("User ID does not match the account owner.");

            var result = await Task.FromResult(stockAccountEntryRepository.Delete(accountId, entryId));
            return Ok(result);
        }

        [HttpPut("UpdateEntry")]
        public async Task<IActionResult> UpdateEntry(StockAccountEntry entry)
        {
            var userId = ApiAuthenticationHelper.GetUserId(User);
            if (userId is null) return BadRequest("User ID is null.");

            var account = await stockAccountRepository.Get(entry.AccountId);
            if (account == null || account.UserId != userId) return BadRequest("User ID does not match the account owner.");

            var result = await Task.FromResult(stockAccountEntryRepository.Update(entry));
            return Ok(result);
        }
    }
}
