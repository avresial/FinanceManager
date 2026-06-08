using FinanceManager.Api.Helpers;
using FinanceManager.Application.Services;
using FinanceManager.Application.Services.Currencies;
using FinanceManager.Application.Services.Exports;
using FinanceManager.Domain.Commands.Account;
using FinanceManager.Domain.Dtos;
using FinanceManager.Domain.Entities.Exports;
using FinanceManager.Domain.Entities.FinancialAccounts.Currencies;
using FinanceManager.Domain.Repositories.Account;
using FinanceManager.Infrastructure.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinanceManager.Api.Controllers.Accounts;

[Authorize]
[Route("api/[controller]")]
[ApiController]
[Tags("Currency Accounts")]
public class CurrencyAccountController(ICurrencyAccountRepository<CurrencyAccount> accountRepository,
    IAccountEntryRepository<CurrencyAccountEntry> accountEntryRepository, ICurrencyEntryProvider currencyEntryProvider,
    IUserPlanVerifier userPlanVerifier,
    IAccountCsvExportService<CurrencyAccountExportDto> currencyAccountCsvExportService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(List<CurrencyAccountDto>))]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get()
    {
        var userId = ApiAuthenticationHelper.GetUserId(User);
        var accounts = await accountRepository.GetAvailableAccounts(userId).ToListAsync();

        return accounts.Count == 0 ? NotFound() : Ok(accounts);
    }

    [HttpGet("{accountId:int}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(CurrencyAccountDto))]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Get(int accountId)
    {
        var account = await accountRepository.Get(accountId);
        if (account is null) return NotFound();
        if (!ApiAuthenticationHelper.IsAccountOwner(User, account.UserId)) return Forbid();

        return Ok(account);
    }

    [HttpGet("{accountId:int}&{startDate:DateTime}&{endDate:DateTime}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(CurrencyAccountDto))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Get(int accountId, DateTime startDate, DateTime endDate, [FromQuery] int minimumEntryCount = 0)
    {
        var account = await accountRepository.Get(accountId);

        if (account is null) return NotFound();
        if (!ApiAuthenticationHelper.IsAccountOwner(User, account.UserId)) return Forbid();
        if (startDate > endDate) return BadRequest("Start date cannot be after end date.");
        if (minimumEntryCount < 0) return BadRequest("Minimum entry count cannot be negative.");

        var loadResult = await currencyEntryProvider.GetEntriesAsync(accountId, startDate, endDate, minimumEntryCount);

        return Ok(await CreateDtoAsync(account, loadResult.Entries, loadResult.EffectiveStartDate, endDate));
    }

    [HttpGet("{accountId:int}/GetInitialTransactionHistory")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(CurrencyAccountDto))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetInitialTransactionHistory(int accountId, [FromQuery] DateTime startDate,
        [FromQuery] DateTime endDate, [FromQuery] int minimumEntriesCount = 100)
    {
        var account = await accountRepository.Get(accountId);

        if (account is null) return NotFound();
        if (!ApiAuthenticationHelper.IsAccountOwner(User, account.UserId)) return Forbid();
        if (startDate > endDate) return BadRequest("Start date cannot be after end date.");
        if (minimumEntriesCount < 0) return BadRequest("Minimum entries count cannot be negative.");

        var loadResult = await currencyEntryProvider.GetEntriesAsync(accountId, startDate, endDate, minimumEntriesCount);

        return Ok(await CreateDtoAsync(account, loadResult.Entries, loadResult.EffectiveStartDate, endDate));
    }

    [HttpGet("{accountId:int}/entries")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(CurrencyAccountDto))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Get(int accountId, [FromQuery] DateTime date, [FromQuery] int count, [FromQuery] bool olderThenDate = true)
    {
        var account = await accountRepository.Get(accountId);

        if (account is null) return NotFound();
        if (!ApiAuthenticationHelper.IsAccountOwner(User, account.UserId)) return Forbid();
        if (count <= 0) return BadRequest("Count must be greater than 0.");

        var entries = await accountEntryRepository.Get(accountId, date, count, olderThenDate);
        var nextOlderReferenceDate = entries.Any() ? entries.Min(x => x.PostingDate) : date;
        var nextYoungerReferenceDate = entries.Any() ? entries.Max(x => x.PostingDate) : date;

        return Ok(await CreateDtoAsync(account, entries, nextOlderReferenceDate, nextYoungerReferenceDate));
    }

    [HttpPost]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(int))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Add(AddAccount addAccount)
    {
        var userId = ApiAuthenticationHelper.GetUserId(User);

        if (!await userPlanVerifier.CanAddMoreAccounts(userId))
            return BadRequest("Too many accounts. In order to add this account upgrade to higher tier or delete existing one.");

        return Ok(await accountRepository.Add(userId, addAccount.AccountName));
    }

    [HttpPut]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(int))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Update(UpdateAccount updateAccount)
    {
        var account = await accountRepository.Get(updateAccount.AccountId);

        if (account is null || !ApiAuthenticationHelper.IsAccountOwner(User, account.UserId)) return BadRequest();
        return Ok(await accountRepository.Update(updateAccount.AccountId, updateAccount.AccountName, updateAccount.AccountType));
    }

    [HttpDelete("{accountId:int}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(bool))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Delete(int accountId)
    {
        var account = await accountRepository.Get(accountId);
        if (account is null || !ApiAuthenticationHelper.IsAccountOwner(User, account.UserId)) return BadRequest();

        await accountEntryRepository.Delete(accountId);
        return Ok(await accountRepository.Delete(accountId));
    }

    [HttpGet("export/{accountId:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ExportCsv(int accountId, [FromQuery] DateTime startDate, [FromQuery] DateTime endDate, CancellationToken cancellationToken)
    {
        var account = await accountRepository.Get(accountId);
        if (account is null) return NotFound();
        if (!ApiAuthenticationHelper.IsAccountOwner(User, account.UserId)) return Forbid();

        var csv = await currencyAccountCsvExportService.GetExportResults(account.UserId, accountId, startDate, endDate, cancellationToken);
        var fileName = $"currency-account-{accountId}-{startDate:yyyyMMdd}-{endDate:yyyyMMdd}.csv";

        return File(System.Text.Encoding.UTF8.GetBytes(csv), "text/csv", fileName);
    }

    private async Task<CurrencyAccountDto> CreateDtoAsync(CurrencyAccount account, IEnumerable<CurrencyAccountEntry> entries,
        DateTime nextOlderReferenceDate, DateTime nextYoungerReferenceDate)
    {
        var orderedEntries = entries
            .OrderByDescending(x => x.PostingDate)
            .ThenByDescending(x => x.EntryId)
            .ToList();

        return account.ToDto(
            await accountEntryRepository.GetNextOlder(account.AccountId, nextOlderReferenceDate),
            await accountEntryRepository.GetNextYounger(account.AccountId, nextYoungerReferenceDate),
            orderedEntries);
    }
}