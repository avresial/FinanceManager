using FinanceManager.Api.Helpers;
using FinanceManager.Application.Services;
using FinanceManager.Domain.Entities.Imports;
using FinanceManager.Domain.Repositories.Account;
using FinanceManager.Domain.Entities.Accounts;
using FinanceManager.Infrastructure.Dtos; // added for BankDataImportDto
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinanceManager.Api.Controllers.Accounts;

[Authorize]
[Route("api/[controller]")]
[ApiController]
public class BankAccountImportController(IBankAccountImportService importService, IBankAccountRepository<BankAccount> bankAccountRepository)
    : ControllerBase
{
    [HttpPost("ImportBankEntries")]
    public async Task<IActionResult> ImportBankEntries([FromBody] BankDataImportDto importDto)
    {
        if (importDto is null) return BadRequest("No import data provided.");
        var userId = ApiAuthenticationHelper.GetUserId(User);
        var domainEntries = importDto.Entries.Select(e => new BankEntryImport(e.PostingDate, e.ValueChange));
        var domainResult = await importService.ImportEntries(userId, importDto.AccountId, domainEntries);
        return Ok(domainResult);
    }

    [HttpPost("ResolveImportConflicts")]
    public async Task<IActionResult> ResolveImportConflicts([FromBody] IEnumerable<ResolvedImportConflict> resolvedConflicts)
    {
        if (resolvedConflicts is null) return BadRequest("No resolved conflicts provided.");
        var userId = ApiAuthenticationHelper.GetUserId(User);

        foreach (var accountId in resolvedConflicts.Select(rc => rc.AccountId).Distinct())
        {
            var account = await bankAccountRepository.Get(accountId);
            if (account is null || account.UserId != userId)
                return Forbid("Account not found or access denied.");
        }

        await importService.ApplyResolvedConflicts(resolvedConflicts);
        return Ok();
    }
}