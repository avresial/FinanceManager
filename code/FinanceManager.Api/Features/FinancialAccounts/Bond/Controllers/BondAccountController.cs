using FinanceManager.Api.Features.FinancialAccounts.Bond.Extensions;
using FinanceManager.Api.Shared.Helpers;
using FinanceManager.Application.Alerts.Services;
using FinanceManager.Application.FinancialAccounts.Bond;
using FinanceManager.Application.FinancialAccounts.Shared.Exports;
using FinanceManager.Application.Identity.Users;
using FinanceManager.Contracts.FinancialAccounts.Bond;
using FinanceManager.Domain.Dashboard.Services;
using FinanceManager.Domain.FinancialAccounts.Bond.Entities;
using FinanceManager.Domain.FinancialAccounts.Bond.Exports;
using FinanceManager.Domain.FinancialAccounts.Bond.Repositories;
using FinanceManager.Domain.FinancialAccounts.Shared.Commands;
using FinanceManager.Domain.FinancialAccounts.Shared.Exports;
using FinanceManager.Domain.FinancialAccounts.Shared.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinanceManager.Api.Features.FinancialAccounts.Bond.Controllers;

[Authorize]
[Route("api/[controller]")]
[ApiController]
[Tags("Bond Accounts")]
public class BondAccountController(IAccountRepository<BondAccount> bondAccountRepository,
    IBondAccountEntryRepository<BondAccountEntry> bondAccountEntryRepository,
    IAccountCsvExportService<BondAccountExportDto> bondAccountCsvExportService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(List<BondAccountDto>))]
    public async Task<IActionResult> Get()
    {
        var userId = ApiAuthenticationHelper.GetUserId(User);
        var accounts = await bondAccountRepository.GetAvailableAccounts(userId).ToListAsync();

        // An empty list is a normal answer for users without accounts, not a 404 logged on every page.
        return Ok(accounts);
    }

    [HttpGet("{accountId:int}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(BondAccountDto))]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Get(int accountId)
    {
        var account = await bondAccountRepository.Get(accountId);
        if (account is null) return NotFound();
        if (!ApiAuthenticationHelper.IsAccountOwner(User, account.UserId)) return Forbid();

        return Ok(account);
    }

    [HttpGet("{accountId:int}/{startDate:DateTime}/{endDate:DateTime}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(BondAccountDto))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Get(int accountId, DateTime startDate, DateTime endDate, [FromQuery] int minimumEntryCount = 0)
    {
        return await GetAccountWithEntries(accountId, startDate, endDate, minimumEntryCount);
    }

    private async Task<IActionResult> GetAccountWithEntries(int accountId, DateTime startDate, DateTime endDate, int minimumEntryCount)
    {
        var account = await bondAccountRepository.Get(accountId);

        if (account is null) return NotFound();
        if (!ApiAuthenticationHelper.IsAccountOwner(User, account.UserId)) return Forbid();
        if (startDate > endDate) return BadRequest("Start date cannot be after end date.");
        if (minimumEntryCount < 0) return BadRequest("Minimum entry count cannot be negative.");

        var (entries, effectiveStartDate) = await bondAccountEntryRepository.GetEntriesWithMinimumCount(accountId, startDate, endDate, minimumEntryCount);

        return Ok(await CreateDtoAsync(account, entries, effectiveStartDate, endDate));
    }

    [HttpGet("{accountId:int}/entries")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(BondAccountDto))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Get(int accountId, [FromQuery] DateTime date, [FromQuery] int count, [FromQuery] bool olderThenDate = true)
    {
        var account = await bondAccountRepository.Get(accountId);

        if (account is null) return NotFound();
        if (!ApiAuthenticationHelper.IsAccountOwner(User, account.UserId)) return Forbid();
        if (count <= 0) return BadRequest("Count must be greater than 0.");

        var entries = await bondAccountEntryRepository.Get(accountId, date, count, olderThenDate);
        var nextOlderReferenceDate = entries.Any() ? entries.Min(x => x.PostingDate) : date;
        var nextYoungerReferenceDate = entries.Any() ? entries.Max(x => x.PostingDate) : date;

        return Ok(await CreateDtoAsync(account, entries, nextOlderReferenceDate, nextYoungerReferenceDate));
    }

    [HttpGet("export/{accountId:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ExportCsv(int accountId, [FromQuery] DateTime startDate, [FromQuery] DateTime endDate, CancellationToken cancellationToken)
    {
        var account = await bondAccountRepository.Get(accountId);
        if (account is null) return NotFound();
        if (!ApiAuthenticationHelper.IsAccountOwner(User, account.UserId)) return Forbid();

        var csv = await bondAccountCsvExportService.GetExportResults(account.UserId, accountId, startDate, endDate, cancellationToken);
        var fileName = $"bond-account-{accountId}-{startDate:yyyyMMdd}-{endDate:yyyyMMdd}.csv";

        return File(System.Text.Encoding.UTF8.GetBytes(csv), "text/csv", fileName);
    }

    private async Task<BondAccountDto> CreateDtoAsync(BondAccount account, IEnumerable<BondAccountEntry> entries,
        DateTime nextOlderReferenceDate, DateTime nextYoungerReferenceDate)
    {
        var orderedEntries = entries
            .OrderByDescending(x => x.PostingDate)
            .ThenByDescending(x => x.EntryId)
            .ToList();

        return account.ToDto(
            await bondAccountEntryRepository.GetNextOlder(account.AccountId, nextOlderReferenceDate),
            await bondAccountEntryRepository.GetNextYounger(account.AccountId, nextYoungerReferenceDate),
            orderedEntries);
    }
}