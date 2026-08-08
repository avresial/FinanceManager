using FinanceManager.Api;
using FinanceManager.Api.Shared.Helpers;
using FinanceManager.Application.FinancialAccounts.Bond.Import;
using FinanceManager.Domain.Dashboard.Services;
using FinanceManager.Domain.FinancialAccounts.Bond.Dtos;
using FinanceManager.Domain.FinancialAccounts.Bond.Entities;
using FinanceManager.Domain.FinancialAccounts.Bond.Imports;
using FinanceManager.Domain.FinancialAccounts.Shared.Dtos;
using FinanceManager.Domain.FinancialAccounts.Shared.Imports;
using FinanceManager.Domain.FinancialAccounts.Shared.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinanceManager.Api.Features.FinancialAccounts.Bond.Controllers;

[Authorize]
[Route("api/[controller]")]
[ApiController]
[Tags("Bond Imports")]
public class BondAccountImportController(IBondAccountImportService importService, IAccountRepository<BondAccount> accountRepository,
    ICacheInvalidator dashboardCacheInvalidator)
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
        var account = await accountRepository.Get(importDto.AccountId, HttpContext.RequestAborted);
        if (account is null || !ApiAuthenticationHelper.IsAccountOwner(User, account.UserId))
            return Forbid();

        var domainEntries = importDto.Entries.Select(e => new BondEntryImport(e.PostingDate, e.ValueChange, e.BondDetailsId));
        var domainResult = await importService.ImportEntries(userId, importDto.AccountId, domainEntries, HttpContext.RequestAborted);
        await dashboardCacheInvalidator.InvalidateUser(userId);
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
            var account = await accountRepository.Get(accountId, HttpContext.RequestAborted);
            if (account is null || !ApiAuthenticationHelper.IsAccountOwner(User, account.UserId))
                return Forbid();
        }

        await importService.ApplyResolvedConflicts(resolvedConflicts, HttpContext.RequestAborted);
        await dashboardCacheInvalidator.InvalidateUser(userId);
        return Ok();
    }
}