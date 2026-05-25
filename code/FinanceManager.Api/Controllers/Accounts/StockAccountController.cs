using FinanceManager.Api.Helpers;
using FinanceManager.Application.Commands.Account;
using FinanceManager.Application.Services.Exports;
using FinanceManager.Application.Services.Stocks;
using FinanceManager.Domain.Commands.Account;
using FinanceManager.Domain.Entities.Exports;
using FinanceManager.Domain.Entities.Stocks;
using FinanceManager.Domain.Repositories.Account;
using FinanceManager.Domain.ValueObjects;
using FinanceManager.Infrastructure.Dtos;
using FinanceManager.Infrastructure.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinanceManager.Api.Controllers.Accounts;

[Authorize]
[Route("api/[controller]")]
[ApiController]
[Tags("Stock Accounts")]
public class StockAccountController(IAccountRepository<StockAccount> stockAccountRepository,
    IStockAccountEntryRepository<StockAccountEntry> stockAccountEntryRepository,
    IStockEntryProvider stockEntryProvider,
    IAccountCsvExportService<StockAccountExportDto> stockAccountCsvExportService) : ControllerBase
{

    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(List<StockAccountDto>))]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<List<AvailableAccount>>> Get()
    {
        var accounts = await stockAccountRepository.GetAvailableAccounts(ApiAuthenticationHelper.GetUserId(User))
            .ToListAsync();

        if (accounts.Count == 0) return NotFound();

        return Ok(accounts ?? []);
    }

    [HttpGet("{accountId:int}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(StockAccountDto))]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Get(int accountId)
    {
        var account = await stockAccountRepository.Get(accountId);
        if (account is null) return NoContent();
        if (account.UserId != ApiAuthenticationHelper.GetUserId(User))
            return Forbid("User does not own this account.");

        return Ok(account);
    }

    [HttpGet("{accountId:int}&{startDate:DateTime}&{endDate:DateTime}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(StockAccountDto))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Get(int accountId, DateTime startDate, DateTime endDate, [FromQuery] int minimumEntryCount = 0)
    {
        var userId = ApiAuthenticationHelper.GetUserId(User);

        var account = await stockAccountRepository.Get(accountId);
        if (account is null) return NotFound();
        if (account.UserId != userId) return Forbid("User ID does not match the account owner.");
        if (minimumEntryCount < 0) return BadRequest("Minimum entry count cannot be negative.");

        var loadResult = await stockEntryProvider.GetEntriesAsync(accountId, startDate, endDate, minimumEntryCount);

        return Ok(await CreateDtoAsync(account, loadResult.Entries, loadResult.EffectiveStartDate, endDate));
    }

    [HttpGet("{accountId:int}/entries")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(StockAccountDto))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Get(int accountId, [FromQuery] DateTime date, [FromQuery] int count, [FromQuery] bool olderThenDate = true)
    {
        var userId = ApiAuthenticationHelper.GetUserId(User);

        var account = await stockAccountRepository.Get(accountId);
        if (account is null) return NotFound();
        if (account.UserId != userId) return Forbid("User ID does not match the account owner.");
        if (count <= 0) return BadRequest("Count must be greater than 0.");

        var entries = await stockAccountEntryRepository.Get(accountId, date, count, olderThenDate);
        var nextOlderReferenceDate = entries.Any() ? entries.Min(x => x.PostingDate) : date;
        var nextYoungerReferenceDate = entries.Any() ? entries.Max(x => x.PostingDate) : date;

        return Ok(await CreateDtoAsync(account, entries, nextOlderReferenceDate, nextYoungerReferenceDate));
    }

    private async Task<StockAccountDto> CreateDtoAsync(StockAccount account, IEnumerable<StockAccountEntry> entries,
        DateTime nextOlderReferenceDate, DateTime nextYoungerReferenceDate)
    {
        var orderedEntries = entries
            .OrderByDescending(x => x.PostingDate)
            .ThenByDescending(x => x.EntryId)
            .ToList();

        return new StockAccountDto()
        {
            AccountId = account.AccountId,
            UserId = account.UserId,
            Name = account.Name,
            NextOlderEntries = (await stockAccountEntryRepository.GetNextOlder(account.AccountId, nextOlderReferenceDate)).ToDictionary(x => x.Key, x => x.Value.ToDto()),
            NextYoungerEntries = (await stockAccountEntryRepository.GetNextYounger(account.AccountId, nextYoungerReferenceDate)).ToDictionary(x => x.Key, x => x.Value.ToDto()),
            Entries = orderedEntries.Select(x => x.ToDto())
        };
    }

    [HttpPost("Add")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Add(AddAccount addAccount) =>
        Ok(await stockAccountRepository.Add(ApiAuthenticationHelper.GetUserId(User), addAccount.AccountName));

    [HttpPut("Update")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Update(UpdateAccount updateAccount)
    {
        var account = await stockAccountRepository.Get(updateAccount.AccountId);
        if (account is null || account.UserId != ApiAuthenticationHelper.GetUserId(User))
            return Forbid("User ID does not match the account owner.");

        var result = await stockAccountRepository.Update(updateAccount.AccountId, updateAccount.AccountName);
        return Ok(result);
    }


    [HttpDelete("Delete/{accountId:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Delete(int accountId)
    {
        var account = await stockAccountRepository.Get(accountId);
        if (account is null) return NotFound("Account not found.");
        if (account.UserId != ApiAuthenticationHelper.GetUserId(User))
            return Forbid("User does not own this account.");

        await stockAccountEntryRepository.Delete(accountId);
        return Ok(await stockAccountRepository.Delete(accountId));
    }

    [HttpGet("export/{accountId:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ExportCsv(int accountId, [FromQuery] DateTime startDate, [FromQuery] DateTime endDate, CancellationToken cancellationToken)
    {
        var account = await stockAccountRepository.Get(accountId);
        if (account is null) return NotFound();
        if (account.UserId != ApiAuthenticationHelper.GetUserId(User))
            return Forbid("User does not own this account.");

        var csv = await stockAccountCsvExportService.GetExportResults(account.UserId, accountId, startDate, endDate, cancellationToken);
        var fileName = $"stock-account-{accountId}-{startDate:yyyyMMdd}-{endDate:yyyyMMdd}.csv";

        return File(System.Text.Encoding.UTF8.GetBytes(csv), "text/csv", fileName);
    }
}