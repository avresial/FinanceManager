using FinanceManager.Api;
using FinanceManager.Api.Helpers;
using FinanceManager.Application.FinancialAccounts.Bond.Import;
using FinanceManager.Domain.Dtos;
using FinanceManager.Domain.Entities.Imports;
using FinanceManager.Domain.FinancialAccounts.Bond.Dtos;
using FinanceManager.Domain.FinancialAccounts.Bond.Entities;
using FinanceManager.Domain.FinancialAccounts.Bond.Imports;
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
    [HttpPost(RequestBodySizeLimits.BondImportPath)]
    [RequestSizeLimit(FinanceManager.Api.RequestBodySizeLimits.ImportEndpointBytes)]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(object))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ImportBondEntries([FromBody] BondDataImportDto importDto)
    {
        if (importDto is null)
            return BadRequest("No import data provided.");

        var userId = ApiAuthenticationHelper.GetUserId(User);
        var account = await accountRepository.Get(importDto.AccountId);
        if (account is null || !ApiAuthenticationHelper.IsAccountOwner(User, account.UserId))
            return Forbid();

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
            if (account is null || !ApiAuthenticationHelper.IsAccountOwner(User, account.UserId))
                return Forbid();
        }

        await importService.ApplyResolvedConflicts(resolvedConflicts);
        return Ok();
    }
}