using FinanceManager.Api;
using FinanceManager.Api.Helpers;
using FinanceManager.Application.FinancialAccounts.Stock.Import;
using FinanceManager.Domain.FinancialAccounts.Shared.Dtos;
using FinanceManager.Domain.FinancialAccounts.Shared.Imports;
using FinanceManager.Domain.FinancialAccounts.Shared.Repositories;
using FinanceManager.Domain.FinancialAccounts.Stock.Dtos;
using FinanceManager.Domain.FinancialAccounts.Stock.Entities;
using FinanceManager.Domain.FinancialAccounts.Stock.Imports;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinanceManager.Api.Controllers.Accounts;

[Authorize]
[Route("api/[controller]")]
[ApiController]
[Tags("Stock Imports")]
public class StockAccountImportController(IStockAccountImportService importService, IAccountRepository<StockAccount> accountRepository)
    : ControllerBase
{
    [HttpPost(RequestBodySizeLimits.StockImportPath)]
    [RequestSizeLimit(FinanceManager.Api.RequestBodySizeLimits.ImportEndpointBytes)]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(object))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ImportStockEntries([FromBody] StockDataImportDto importDto)
    {
        if (importDto is null) return BadRequest("No import data provided.");
        var userId = ApiAuthenticationHelper.GetUserId(User);
        var account = await accountRepository.Get(importDto.AccountId);
        if (account is null || !ApiAuthenticationHelper.IsAccountOwner(User, account.UserId))
            return Forbid();

        var domainEntries = importDto.Entries.Select(e => new StockEntryImport(e.PostingDate, e.ValueChange, e.Ticker));
        var domainResult = await importService.ImportEntries(userId, importDto.AccountId, domainEntries);
        return Ok(domainResult);
    }

    [HttpPost("ResolveImportConflicts")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ResolveImportConflicts([FromBody] IEnumerable<ResolvedStockImportConflict> resolvedConflicts)
    {
        if (resolvedConflicts is null) return BadRequest("No resolved conflicts provided.");
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