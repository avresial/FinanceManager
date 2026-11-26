using FinanceManager.Api.Features.Dashboard.Extensions;
using FinanceManager.Api.Features.FinancialAccounts.Currencies.Hubs;
using FinanceManager.Api.Features.FinancialAccounts.Currencies.Services;
using FinanceManager.Api.Features.Labels.Services;
using FinanceManager.Api.Features.Transactions.Shared.Validation;
using FinanceManager.Api.Shared.Helpers;
using FinanceManager.Application.Alerts.Services;
using FinanceManager.Application.FinancialAccounts.Currencies.Import;
using FinanceManager.Application.FinancialAccounts.Shared.Exports;
using FinanceManager.Application.Identity.Users;
using FinanceManager.Application.TransactionRules.Services;
using FinanceManager.Contracts.Dashboard;
using FinanceManager.Contracts.Transactions.Cash;
using FinanceManager.Domain.Dashboard.Dtos;
using FinanceManager.Domain.Dashboard.Services;
using FinanceManager.Domain.FinancialAccounts.Currencies.Dtos;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.FinancialAccounts.Currencies.Exports;
using FinanceManager.Domain.FinancialAccounts.Currencies.Repositories;
using FinanceManager.Domain.FinancialAccounts.Shared.Entities;
using FinanceManager.Domain.FinancialAccounts.Shared.Repositories;
using FinanceManager.Domain.Identity.Services;
using FinanceManager.Domain.Labels.Repositories;
using FinanceManager.Domain.Transactions.Cash.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.SignalR;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace FinanceManager.Api.Features.Transactions.Cash.Controllers;

/// <summary>
/// The caller's cash entries pooled per currency. An entry belongs to its owner (<c>UserId</c>) and needs no account;
/// an entry or job of someone else is reported as not found.
/// </summary>
[Authorize]
[ApiController]
[Route("api/transactions/cash")]
[Tags("Owner Cash Transactions")]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
public class CashTransactionsController(
    IAccountEntryRepository<CurrencyAccountEntry> entryRepository,
    ICurrencyRepository currencyRepository,
    IFinancialLabelsRepository labelsRepository,
    IUserPlanVerifier userPlanVerifier,
    ITransactionRuleService transactionRuleService,
    IFinancialAlertService financialAlertService,
    ICashTransactionReader cashTransactionReader,
    ILabelSetterChannel labelSetterChannel,
    IGuestSessionAccessor guestSessionAccessor,
    IAccountCsvExportService<CurrencyAccountExportDto> csvExportService) : ControllerBase
{
    private const int _maxBatchEntryIds = 100;

    /// <summary>The caller's entries with exactly these ids (1 to 100), newest first. Ids that are missing or belong to someone else are skipped.</summary>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<CurrencyTransactionLogItemDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetByIds([FromQuery] long[] entryIds, CancellationToken cancellationToken = default)
    {
        if (!ApiAuthenticationHelper.TryGetOwnerId(User, out var ownerId)) return Forbid();

        var ids = entryIds.Distinct().ToArray();
        if (ids.Length is 0 or > _maxBatchEntryIds || ids.Any(id => id <= 0))
            return BadRequest($"Provide between 1 and {_maxBatchEntryIds} positive entry ids.");

        var entries = await cashTransactionReader.GetByIds(ownerId, ids, cancellationToken);
        return Ok(entries.Select(entry => entry.ToLogItemDto()).ToList());
    }

    [HttpPost]
    [ProducesResponseType<CurrencyTransactionLogItemDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create(CreateCashEntryRequest request, CancellationToken cancellationToken = default)
    {
        if (!ApiAuthenticationHelper.TryGetOwnerId(User, out var ownerId)) return Forbid();

        OwnerEntryRequestValidation.ValidateFields(ModelState, request.PostingDate, request.Description, descriptionRequired: true, request.ContractorDetails);
        if (!ModelState.IsValid)
            return ValidationProblem(statusCode: StatusCodes.Status400BadRequest, modelStateDictionary: ModelState);
        if (await currencyRepository.GetCurrency(request.CurrencyId, cancellationToken) is not Currency currency) return BadRequest("Unknown currency.");
        if (await OwnerEntryRequestValidation.ResolveLabels(labelsRepository, request.LabelIds, cancellationToken) is not List<FinancialLabel> labels)
            return BadRequest("Unknown label ID.");
        if (!await userPlanVerifier.CanAddMoreEntries(ownerId))
            return BadRequest("Too many entries. In order to add this entry upgrade to higher tier or delete existing one.");

        var entry = new CurrencyAccountEntry(null, 0, request.PostingDate, 0, request.ValueChange)
        {
            UserId = ownerId,
            CurrencyId = currency.Id,
            Description = request.Description,
            ContractorDetails = request.ContractorDetails,
            Labels = labels,
        };
        await transactionRuleService.ApplyToEntryAsync(ownerId, entry, cancellationToken);

        if (!await entryRepository.Add(entry, cancellationToken)) return BadRequest("Cannot add this entry.");

        await QueueLabeling(ownerId, entry, cancellationToken);
        await financialAlertService.EvaluateAlertsAsync(ownerId, cancellationToken);
        return await Persisted(ownerId, entry.EntryId, currency.ShortName, cancellationToken);
    }

    [HttpPut("{entryId:long}")]
    [ProducesResponseType<CurrencyTransactionLogItemDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(long entryId, UpdateCashEntryRequest request, CancellationToken cancellationToken = default)
    {
        if (!ApiAuthenticationHelper.TryGetOwnerId(User, out var ownerId)) return Forbid();

        OwnerEntryRequestValidation.ValidateFields(ModelState, request.PostingDate, request.Description, descriptionRequired: true, request.ContractorDetails);
        if (!ModelState.IsValid)
            return ValidationProblem(statusCode: StatusCodes.Status400BadRequest, modelStateDictionary: ModelState);

        var existing = await entryRepository.GetByOwner(ownerId, entryId, cancellationToken);
        if (existing is null) return NotFound();

        // Omitted label ids keep the labels; an explicit list, even an empty one, replaces them.
        var labels = request.LabelIds is null
            ? existing.Labels.ToList()
            : await OwnerEntryRequestValidation.ResolveLabels(labelsRepository, request.LabelIds, cancellationToken);
        if (labels is null) return BadRequest("Unknown label ID.");

        // The entry keeps its persisted currency, and its owner and account link never come from the request.
        var entry = new CurrencyAccountEntry(null, entryId, request.PostingDate, 0, request.ValueChange)
        {
            UserId = ownerId,
            CurrencyId = existing.CurrencyId,
            Description = request.Description,
            ContractorDetails = request.ContractorDetails,
            Labels = labels,
        };
        await transactionRuleService.ApplyToEntryAsync(ownerId, entry, cancellationToken);

        if (!await entryRepository.UpdateByOwner(ownerId, entry, cancellationToken)) return NotFound();

        await financialAlertService.EvaluateAlertsAsync(ownerId, cancellationToken);
        return await Persisted(ownerId, entryId, currencyCode: null, cancellationToken);
    }

    [HttpDelete("{entryId:long}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(long entryId, CancellationToken cancellationToken = default)
    {
        if (!ApiAuthenticationHelper.TryGetOwnerId(User, out var ownerId)) return Forbid();
        if (!await entryRepository.DeleteByOwner(ownerId, entryId, cancellationToken)) return NotFound();

        await financialAlertService.EvaluateAlertsAsync(ownerId, cancellationToken);
        return NoContent();
    }

    [HttpPost("import")]
    [RequestSizeLimit(RequestBodySizeLimits.ImportEndpointBytes)]
    [ProducesResponseType<CurrencyImportJobStartResponseDto>(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> StartImport(
        [FromQuery, BindRequired] int currencyId,
        [FromBody] IReadOnlyList<CurrencyEntryImportRecordDto> entries,
        [FromServices] ICurrencyImportJobStore jobStore,
        [FromServices] ICurrencyImportJobChannel jobChannel,
        CancellationToken cancellationToken = default)
    {
        if (!ApiAuthenticationHelper.TryGetOwnerId(User, out var ownerId)) return Forbid();
        if (entries is null || entries.Count == 0) return BadRequest("No import data provided.");
        if (await currencyRepository.GetCurrency(currencyId, cancellationToken) is null) return BadRequest("Unknown currency.");

        var jobId = jobStore.CreateOwnerJob(ownerId, currencyId, entries.Count);
        await jobChannel.QueueJob(new CurrencyImportJobRequest(jobId, ownerId, currencyId, entries, guestSessionAccessor.GuestUserId), cancellationToken);

        return Accepted(new CurrencyImportJobStartResponseDto(jobId));
    }

    [HttpGet("export")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Export(
        [FromQuery, BindRequired] int currencyId,
        [FromQuery, BindRequired] DateTime startDate,
        [FromQuery, BindRequired] DateTime endDate,
        CancellationToken cancellationToken = default)
    {
        if (!ApiAuthenticationHelper.TryGetOwnerId(User, out var ownerId)) return Forbid();

        var start = TransactionLogQuery.AsUtc(startDate);
        var end = TransactionLogQuery.AsUtc(endDate);
        if (start > end) return BadRequest("Start must be on or before end.");
        if (await currencyRepository.GetCurrency(currencyId, cancellationToken) is not Currency currency) return BadRequest("Unknown currency.");

        var csv = await csvExportService.GetOwnerExportResults(ownerId, currencyId, start, end, cancellationToken);
        var fileName = $"cash-{currency.ShortName.ToLowerInvariant()}-{start:yyyyMMdd}-{end:yyyyMMdd}.csv";
        return File(Encoding.UTF8.GetBytes(csv), "text/csv", fileName);
    }

    // Guest sandboxes are ephemeral and never reach the shared label worker; only real owners' unlabelled, described entries are queued.
    private async Task QueueLabeling(int ownerId, CurrencyAccountEntry entry, CancellationToken cancellationToken)
    {
        if (guestSessionAccessor.IsGuest || ownerId <= 0 || entry.EntryId == 0 || entry.Labels.Count != 0) return;
        if (string.IsNullOrWhiteSpace(entry.Description) && string.IsNullOrWhiteSpace(entry.ContractorDetails)) return;

        await labelSetterChannel.QueueEntries(ownerId, [entry.EntryId], cancellationToken);
    }

    // The exact row the write produced, read back by its generated id with its labels.
    private async Task<IActionResult> Persisted(int ownerId, long entryId, string? currencyCode, CancellationToken cancellationToken)
    {
        var saved = await entryRepository.GetByOwner(ownerId, entryId, cancellationToken);
        if (saved is null) return NotFound();

        currencyCode ??= (await currencyRepository.GetCurrency(saved.CurrencyId, cancellationToken))?.ShortName;
        return Ok(saved.ToLogItemDto(currencyCode));
    }

    [HttpGet("import/jobs/{jobId:guid}")]
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

    [HttpPost("import/jobs/{jobId:guid}/resolve")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(CurrencyImportJobStatusDto))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ResolveAsyncImportConflicts(
        Guid jobId,
        [FromBody] CurrencyImportResolveConflictsRequestDto request,
        [FromServices] ICurrencyAccountImportService importService,
        [FromServices] ICacheInvalidator dashboardCacheInvalidator,
        [FromServices] ILogger<CashTransactionsController> logger,
        [FromServices] ICurrencyImportJobStore jobStore,
        [FromServices] IHubContext<CurrencyImportHub> hub)
    {
        if (request is null || request.JobId != jobId || request.Decisions.Count == 0)
            return BadRequest("Provide conflict decisions for the requested job.");

        if (!ApiAuthenticationHelper.TryGetOwnerId(User, out var userId)) return Forbid();

        if (!jobStore.TryGetCurrencyId(jobId, userId, out var currencyId))
            return NotFound("Import job not found.");

        if (request.Decisions.Any(decision => decision.PickImported == decision.PickExisting))
            return BadRequest("Each decision must pick exactly one side.");
        if (!jobStore.TryGetStatus(jobId, userId, out var current) || current is null)
            return NotFound("Import job not found.");
        var unknown = request.Decisions.FirstOrDefault(decision => current.Conflicts.All(conflict => conflict.ConflictId != decision.ConflictId));
        if (unknown is not null)
            return NotFound($"Conflict {unknown.ConflictId} was not found.");

        var anyApplied = false;
        try
        {
            foreach (var decision in request.Decisions)
            {
                if (!jobStore.TryResolveConflict(jobId, userId, decision, out var addition))
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
                    jobStore.TryReopenConflict(jobId, userId, decision.ConflictId);
                    throw;
                }

                if (!added)
                {
                    jobStore.TryReopenConflict(jobId, userId, decision.ConflictId);
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

        if (!jobStore.TryGetStatus(jobId, userId, out var status) || status is null)
            return NotFound("Import job not found.");

        await hub.Clients.Group(CurrencyImportHub.GetJobGroupName(jobId)).SendAsync("ImportStatusUpdated", status);
        return Ok(status);
    }
}