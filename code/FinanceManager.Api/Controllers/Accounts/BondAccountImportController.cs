using FinanceManager.Api.Helpers;
using FinanceManager.Application.Services.Bonds;
using FinanceManager.Domain.Dtos;
using FinanceManager.Domain.Entities.Bonds;
using FinanceManager.Domain.Entities.Imports;
using FinanceManager.Domain.Repositories.Account;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinanceManager.Api.Controllers.Accounts;

[Authorize]
[Route("api/[controller]")]
[ApiController]
[Tags("Bond Imports")]
public class BondAccountImportController(IBondAccountImportService importService, IAccountRepository<BondAccount> accountRepository)
    : ControllerBase
{
    [HttpPost("ImportBondEntries")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(object))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ImportBondEntries([FromBody] BondDataImportDto importDto)
    {
        if (importDto is null)
            return BadRequest("No import data provided.");

        var userId = ApiAuthenticationHelper.GetUserId(User);
        var domainEntries = importDto.Entries.Select(e => new BondEntryImport(e.PostingDate, e.ValueChange, e.BondDetailsId));
        var domainResult = await importService.ImportEntries(userId, importDto.AccountId, domainEntries);
        return Ok(domainResult);
    }

    [HttpPost("ResolveImportConflicts")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ResolveImportConflicts([FromBody] IEnumerable<ResolvedBondImportConflict> resolvedConflicts)
    {
        if (resolvedConflicts is null)
            return BadRequest("No resolved conflicts provided.");

        var userId = ApiAuthenticationHelper.GetUserId(User);

        foreach (var accountId in resolvedConflicts.Select(rc => rc.AccountId).Distinct())
        {
            var account = await accountRepository.Get(accountId);
            if (account is null || account.UserId != userId)
                return Forbid("Account not found or access denied.");
        }

        await importService.ApplyResolvedConflicts(resolvedConflicts);
        return Ok();
    }
}