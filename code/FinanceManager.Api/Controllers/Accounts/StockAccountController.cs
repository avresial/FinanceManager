using FinanceManager.Api.Helpers;
using FinanceManager.Application.Commands.Account;
using FinanceManager.Application.Services.Exports;
using FinanceManager.Domain.Entities.Exports;
using FinanceManager.Domain.Commands.Account;
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
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Get(int accountId, DateTime startDate, DateTime endDate)
    {
        var userId = ApiAuthenticationHelper.GetUserId(User);

        var account = await stockAccountRepository.Get(accountId);
        if (account is null) return NotFound();
        if (account.UserId != userId) return Forbid("User ID does not match the account owner.");

        var entries = await stockAccountEntryRepository.Get(accountId, startDate, endDate).ToListAsync();

        return Ok(new StockAccountDto()
        {
            AccountId = account.AccountId,
            UserId = account.UserId,
            Name = account.Name,
            NextOlderEntries = (await stockAccountEntryRepository.GetNextOlder(accountId, startDate)).ToDictionary(x => x.Key, x => x.Value.ToDto()),
            NextYoungerEntries = (await stockAccountEntryRepository.GetNextYounger(accountId, startDate)).ToDictionary(x => x.Key, x => x.Value.ToDto()),
            Entries = entries.Select(x => x.ToDto())
        });
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