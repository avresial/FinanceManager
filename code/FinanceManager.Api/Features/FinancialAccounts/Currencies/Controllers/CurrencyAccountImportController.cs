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
public class CurrencyAccountImportController(ICurrencyAccountImportService importService,
    ICacheInvalidator dashboardCacheInvalidator,
    IFinancialAlertService financialAlertService,
    ILogger<CurrencyAccountImportController> logger)
    : ControllerBase
{
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

        if (!jobStore.TryGetCurrencyId(request.JobId, userId, out var currencyId))
            return NotFound("Import job not found.");

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
                if (!jobStore.TryResolveConflict(request.JobId, userId, decision, out var addition))
                    return NotFound($"Conflict {decision.ConflictId} was not found.");
                // An already resolved decision comes back empty and stays a no-op.
                if (addition is null)
                    continue;

                bool added;
                try
                {
                    added = await importService.ApplyOwnerConflict(userId, currencyId, addition, HttpContext.RequestAborted);
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
}