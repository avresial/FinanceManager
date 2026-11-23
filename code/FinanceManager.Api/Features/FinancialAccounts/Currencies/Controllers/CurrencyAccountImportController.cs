using FinanceManager.Api;
using FinanceManager.Api.Features.FinancialAccounts.Currencies.Hubs;
using FinanceManager.Api.Features.FinancialAccounts.Currencies.Services;
using FinanceManager.Api.Shared.Helpers;
using FinanceManager.Application.Alerts.Services;
using FinanceManager.Application.FinancialAccounts.Currencies.Import;
using FinanceManager.Domain.Dashboard.Services;
using FinanceManager.Domain.FinancialAccounts.Currencies.Dtos;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.FinancialAccounts.Currencies.Imports;
using FinanceManager.Domain.FinancialAccounts.Currencies.Repositories;
using FinanceManager.Domain.FinancialAccounts.Shared.Dtos;
using FinanceManager.Domain.FinancialAccounts.Shared.Imports;
using FinanceManager.Domain.FinancialAccounts.Shared.Repositories;
using FinanceManager.Domain.Identity.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;

namespace FinanceManager.Api.Features.FinancialAccounts.Currencies.Controllers;

[Authorize]
[Route("api/[controller]")]
[ApiController]
[Tags("Currency Imports")]
public class CurrencyAccountImportController(ICurrencyAccountImportService importService, ICurrencyAccountRepository<CurrencyAccount> accountRepository,
    ICacheInvalidator dashboardCacheInvalidator,
    IFinancialAlertService financialAlertService,
    ILogger<CurrencyAccountImportController> logger)
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
        [FromServices] ICurrencyImportJobChannel jobChannel,
        [FromServices] IGuestSessionAccessor guestSessionAccessor)
    {
        if (importDto is null || importDto.Entries.Count == 0)
            return BadRequest("No import data provided.");

        var userId = ApiAuthenticationHelper.GetUserId(User);
        var account = await accountRepository.Get(importDto.AccountId, HttpContext.RequestAborted);
        if (account is null || !ApiAuthenticationHelper.IsAccountOwner(User, account.UserId))
            return Forbid();

        var jobId = jobStore.CreateJob(userId, importDto.AccountId, importDto.Entries.Count);
        await jobChannel.QueueJob(new CurrencyImportJobRequest(jobId, userId, importDto.AccountId, importDto.Entries, guestSessionAccessor.GuestUserId));

        return Accepted(new CurrencyImportJobStartResponseDto(jobId));
    }

    [HttpGet("ImportStatus/{jobId:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(CurrencyImportJobStatusDto))]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult ImportStatus(
        Guid jobId,
        [FromServices] ICurrencyImportJobStore jobStore)
    {
        if (!ApiAuthenticationHelper.TryGetOwnerId(User, out var userId)) return Forbid();
        if (!jobStore.TryGetStatus(jobId, userId, out var status) || status is null)
            return NotFound("Import job not found.");

        return Ok(status);
    }

    [HttpPost("ResolveAsyncImportConflicts")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(CurrencyImportJobStatusDto))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ResolveAsyncImportConflicts(
        [FromBody] CurrencyImportResolveConflictsRequestDto request,
        [FromServices] ICurrencyImportJobStore jobStore,
        [FromServices] IHubContext<CurrencyImportHub> hub)
    {
        if (request is null || request.Decisions.Count == 0)
            return BadRequest("No conflict decisions provided.");

        if (!ApiAuthenticationHelper.TryGetOwnerId(User, out var userId)) return Forbid();

        // The job's own scope, fixed when it was created, decides how its decisions are saved.
        if (jobStore.TryGetScope(request.JobId, userId, out var scope) && scope is { AccountId: null, CurrencyId: int currencyId })
            return await ResolveOwnerConflicts(request, userId, currencyId, jobStore, hub);

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
            await financialAlertService.EvaluateAlertsAsync(userId, HttpContext.RequestAborted);
        }

        if (!jobStore.TryGetStatus(request.JobId, userId, out var status) || status is null)
            return NotFound("Import job not found.");

        await hub.Clients.Group(CurrencyImportHub.GetJobGroupName(request.JobId))
            .SendAsync("ImportStatusUpdated", status);

        return Ok(status);
    }

    // An owner job's decisions only ever add the reviewed incoming rows: picking the existing side is a skip, and no
    // existing row is deleted through a conflict. A decision that cannot be saved is reopened so it can be retried.
    private async Task<IActionResult> ResolveOwnerConflicts(
        CurrencyImportResolveConflictsRequestDto request,
        int userId,
        int currencyId,
        ICurrencyImportJobStore jobStore,
        IHubContext<CurrencyImportHub> hub)
    {
        if (request.Decisions.Any(decision => decision.PickImported == decision.PickExisting))
            return BadRequest("Each decision must pick exactly one side.");
        if (!jobStore.TryGetStatus(request.JobId, userId, out var current) || current is null)
            return NotFound("Import job not found.");
        var unknown = request.Decisions.FirstOrDefault(decision => current.Conflicts.All(conflict => conflict.ConflictId != decision.ConflictId));
        if (unknown is not null)
            return NotFound($"Conflict {unknown.ConflictId} was not found.");

        var anyApplied = false;
        try
        {
            foreach (var decision in request.Decisions)
            {
                if (!jobStore.TryResolveConflict(request.JobId, userId, decision, out var resolved))
                    return NotFound($"Conflict {decision.ConflictId} was not found.");
                // An already resolved decision comes back empty and stays a no-op.
                if (resolved is not { AddImported: true, ImportData: not null })
                    continue;

                bool added;
                try
                {
                    added = await importService.ApplyOwnerConflict(userId, currencyId, resolved.ImportData, HttpContext.RequestAborted);
                }
                catch
                {
                    jobStore.TryReopenConflict(request.JobId, userId, decision.ConflictId);
                    throw;
                }

                if (!added)
                {
                    jobStore.TryReopenConflict(request.JobId, userId, decision.ConflictId);
                    return Problem(statusCode: StatusCodes.Status500InternalServerError, title: "Could not save the decision.",
                        detail: $"Conflict {decision.ConflictId} could not be saved; check the import status and retry.");
                }

                anyApplied = true;
            }
        }
        catch (InvalidOperationException ex)
        {
            // The plan limit is rechecked for every approved addition.
            return BadRequest(ex.Message);
        }
        finally
        {
            if (anyApplied)
            {
                await dashboardCacheInvalidator.InvalidateUser(userId);
                // Alerts are a side effect: a failure here must not replace the response the decisions earned.
                try
                {
                    await financialAlertService.EvaluateAlertsAsync(userId, CancellationToken.None);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Alert evaluation failed after resolving import conflicts.");
                }
            }
        }

        if (!jobStore.TryGetStatus(request.JobId, userId, out var status) || status is null)
            return NotFound("Import job not found.");

        await hub.Clients.Group(CurrencyImportHub.GetJobGroupName(request.JobId)).SendAsync("ImportStatusUpdated", status);
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
        await financialAlertService.EvaluateAlertsAsync(userId, HttpContext.RequestAborted);
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
        await financialAlertService.EvaluateAlertsAsync(userId, HttpContext.RequestAborted);
        return Ok();
    }
}