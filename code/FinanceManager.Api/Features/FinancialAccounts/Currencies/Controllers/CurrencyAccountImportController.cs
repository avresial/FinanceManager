using FinanceManager.Api;
using FinanceManager.Api.Features.FinancialAccounts.Currencies.Hubs;
using FinanceManager.Api.Features.FinancialAccounts.Currencies.Services;
using FinanceManager.Api.Shared.Helpers;
using FinanceManager.Application.FinancialAccounts.Currencies.Import;
using FinanceManager.Domain.Dashboard.Services;
using FinanceManager.Domain.FinancialAccounts.Currencies.Dtos;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.FinancialAccounts.Currencies.Imports;
using FinanceManager.Domain.FinancialAccounts.Currencies.Repositories;
using FinanceManager.Domain.FinancialAccounts.Shared.Dtos;
using FinanceManager.Domain.FinancialAccounts.Shared.Imports;
using FinanceManager.Domain.FinancialAccounts.Shared.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;

namespace FinanceManager.Api.Features.FinancialAccounts.Currencies.Controllers;

[Authorize]
[Route("api/[controller]")]
[ApiController]
[Tags("Currency Imports")]
public class CurrencyAccountImportController(ICurrencyAccountImportService importService, ICurrencyAccountRepository<CurrencyAccount> accountRepository,
    ICacheInvalidator dashboardCacheInvalidator)
    : ControllerBase
{
    [HttpPost(RequestBodySizeLimits.CurrencyStartAsyncImportPath)]
    [RequestSizeLimit(FinanceManager.Api.RequestBodySizeLimits.ImportEndpointBytes)]
    [ProducesResponseType(StatusCodes.Status202Accepted, Type = typeof(CurrencyImportJobStartResponseDto))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> StartAsyncImport(
        [FromBody] CurrencyDataImportDto importDto,
        [FromServices] ICurrencyImportJobStore jobStore,
        [FromServices] ICurrencyImportJobChannel jobChannel)
    {
        if (importDto is null || importDto.Entries.Count == 0)
            return BadRequest("No import data provided.");

        var userId = ApiAuthenticationHelper.GetUserId(User);
        var account = await accountRepository.Get(importDto.AccountId, HttpContext.RequestAborted);
        if (account is null || !ApiAuthenticationHelper.IsAccountOwner(User, account.UserId))
            return Forbid();

        var jobId = jobStore.CreateJob(userId, importDto.AccountId, importDto.Entries.Count);
        await jobChannel.QueueJob(new CurrencyImportJobRequest(jobId, userId, importDto.AccountId, importDto.Entries));

        return Accepted(new CurrencyImportJobStartResponseDto(jobId));
    }

    [HttpGet("ImportStatus/{jobId:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(CurrencyImportJobStatusDto))]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult ImportStatus(
        Guid jobId,
        [FromServices] ICurrencyImportJobStore jobStore)
    {
        var userId = ApiAuthenticationHelper.GetUserId(User);
        if (!jobStore.TryGetStatus(jobId, userId, out var status) || status is null)
            return NotFound("Import job not found.");

        return Ok(status);
    }

    [HttpPost("ResolveAsyncImportConflicts")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(CurrencyImportJobStatusDto))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ResolveAsyncImportConflicts(
        [FromBody] CurrencyImportResolveConflictsRequestDto request,
        [FromServices] ICurrencyImportJobStore jobStore,
        [FromServices] IHubContext<CurrencyImportHub> hub)
    {
        if (request is null || request.Decisions.Count == 0)
            return BadRequest("No conflict decisions provided.");

        var userId = ApiAuthenticationHelper.GetUserId(User);

        var resolvedConflicts = new List<ResolvedImportConflict>();
        foreach (var decision in request.Decisions)
        {
            if (decision.PickImported == decision.PickExisting)
                return BadRequest("Each decision must pick exactly one side.");

            if (!jobStore.TryResolveConflict(request.JobId, userId, decision, out var resolvedConflict))
                return NotFound($"Conflict {decision.ConflictId} was not found.");

            if (resolvedConflict is not null)
                resolvedConflicts.Add(resolvedConflict);
        }

        if (resolvedConflicts.Count != 0)
        {
            await importService.ApplyResolvedConflicts(resolvedConflicts, HttpContext.RequestAborted);
            await dashboardCacheInvalidator.InvalidateUser(userId);
        }

        if (!jobStore.TryGetStatus(request.JobId, userId, out var status) || status is null)
            return NotFound("Import job not found.");

        await hub.Clients.Group(CurrencyImportHub.GetJobGroupName(request.JobId))
            .SendAsync("ImportStatusUpdated", status);

        return Ok(status);
    }

    [HttpPost(RequestBodySizeLimits.CurrencyImportEntriesPath)]
    [RequestSizeLimit(FinanceManager.Api.RequestBodySizeLimits.ImportEndpointBytes)]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(object))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ImportCurrencyEntries([FromBody] CurrencyDataImportDto importDto)
    {
        if (importDto is null) return BadRequest("No import data provided.");
        var userId = ApiAuthenticationHelper.GetUserId(User);
        var account = await accountRepository.Get(importDto.AccountId, HttpContext.RequestAborted);
        if (account is null || !ApiAuthenticationHelper.IsAccountOwner(User, account.UserId))
            return Forbid();

        var domainEntries = importDto.Entries.Select(e => new CurrencyEntryImport(e.PostingDate, e.ValueChange, e.ContractorDetails, e.Description));
        var domainResult = await importService.ImportEntries(userId, importDto.AccountId, domainEntries, cancellationToken: HttpContext.RequestAborted);
        await dashboardCacheInvalidator.InvalidateUser(userId);
        return Ok(domainResult);
    }

    [HttpPost("ResolveImportConflicts")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ResolveImportConflicts([FromBody] IEnumerable<ResolvedImportConflict> resolvedConflicts)
    {
        if (resolvedConflicts is null) return BadRequest("No resolved conflicts provided.");
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