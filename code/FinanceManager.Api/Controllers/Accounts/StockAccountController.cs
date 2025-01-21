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
    public class StockAccountController(IAccountRepository<StockAccount> bankAccountRepository,
        IAccountEntryRepository<StockAccountEntry> stockAccountEntryRepository) : ControllerBase
    {
        private readonly IAccountRepository<StockAccount> stockAccountRepository = bankAccountRepository;
        private readonly IAccountEntryRepository<StockAccountEntry> stockAccountEntryRepository = stockAccountEntryRepository;

        [HttpGet]
        public async Task<IActionResult> Get()
        {
            var userId = ApiAuthenticationHelper.GetUserId(User);
            if (userId is null) return BadRequest();

            var account = stockAccountRepository.GetAvailableAccounts(userId.Value);

            if (account == null) return NoContent();

            return Ok(account);
        }

        [HttpGet("{accountId:int}")]
        public async Task<IActionResult> Get(int accountId)
        {
            var userId = ApiAuthenticationHelper.GetUserId(User);
            if (userId is null) return BadRequest();

            var account = stockAccountRepository.Get(accountId);

            if (account == null) return NoContent();
            if (account.UserId != userId) return BadRequest();

            return Ok(account);
        }
        [HttpGet("{accountId:int}&{startDate:DateTime}&{endDate:DateTime}")]
        public async Task<IActionResult> Get(int accountId, DateTime startDate, DateTime endDate)
        {
            var userId = ApiAuthenticationHelper.GetUserId(User);
            if (userId is null) return BadRequest();

            var account = stockAccountRepository.Get(accountId);

            if (account == null) return NoContent();
            if (account.UserId != userId) return BadRequest();

            var entries = stockAccountEntryRepository.Get(accountId, startDate, endDate);

            account.Add(entries, false);
            return Ok(account);
        }

        [HttpPost]
        [Route("Add")]
        public async Task<IActionResult> Add(AddAccount addAccount)
        {
            var userId = ApiAuthenticationHelper.GetUserId(User);
            if (!userId.HasValue) return BadRequest();

            return Ok(stockAccountRepository.Add(userId.Value, addAccount.accountName));
        }

        [HttpPut]
        [Route("Update")]
        public async Task<IActionResult> Update(UpdateAccount updateAccount)
        {
            var userId = ApiAuthenticationHelper.GetUserId(User);
            if (userId is null) return BadRequest();

            var account = stockAccountRepository.Get(updateAccount.accountId);

            if (account == null || account.UserId != userId) return BadRequest();
            return Ok(stockAccountRepository.Update(updateAccount.accountId, updateAccount.accountName));
        }


        [HttpDelete]
        [Route("Delete")]
        public async Task<IActionResult> Delete(DeleteAccount deleteAccount)
        {
            var userId = ApiAuthenticationHelper.GetUserId(User);
            if (userId is null) return BadRequest();

            var account = stockAccountRepository.Get(deleteAccount.accountId);

            if (account == null || account.UserId != userId) return BadRequest();
            return Ok(stockAccountRepository.Delete(deleteAccount.accountId));
        }
    }
}
