using FinanceManager.Api.Helpers;
using FinanceManager.Domain.Entities.FinancialAccounts.Currencies;
using FinanceManager.Domain.Entities.MoneyFlowModels;
using FinanceManager.Domain.Repositories.Account;
using FinanceManager.Domain.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace FinanceManager.Api.Controllers;

[Authorize]
[Route("api/[controller]")]
[ApiController]
[Tags("Financial Analysis")]
public class RecurringTransactionDetectorController(
    IRecurringTransactionDetectorService recurringTransactionDetectorService,
    ICurrencyAccountRepository<CurrencyAccount> accountRepository,
    IAccountEntryRepository<CurrencyAccountEntry> accountEntryRepository) : ControllerBase
{
    [HttpGet("Get/{userId:int}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(List<RecurringTransactionResult>))]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Get(int userId, CancellationToken cancellationToken = default)
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var authenticatedUserId))
            return Forbid();

        if (authenticatedUserId != userId)
            return Forbid();

        return Ok(await recurringTransactionDetectorService.GetRecurringTransactions(userId, cancellationToken));
    }

    [HttpPost("Entries/{userId:int}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(List<CurrencyAccountEntry>))]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetEntries(int userId, [FromBody] List<RecurringTransactionEntryReference> entryReferences, CancellationToken cancellationToken = default)
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var authenticatedUserId))
            return Forbid();

        if (authenticatedUserId != userId)
            return Forbid();

        var result = new List<CurrencyAccountEntry>();
        var accountIds = entryReferences.Select(r => r.AccountId).Distinct();

        foreach (var accountId in accountIds)
        {
            var account = await accountRepository.Get(accountId);
            if (account is null || account.UserId != userId) continue;

            foreach (var entryId in entryReferences.Where(r => r.AccountId == accountId).Select(r => r.EntryId))
            {
                var entry = await accountEntryRepository.Get(accountId, entryId);
                if (entry is not null) result.Add(entry);
            }
        }

        return Ok(result);
    }
}
