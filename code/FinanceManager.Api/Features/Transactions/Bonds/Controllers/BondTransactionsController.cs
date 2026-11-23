using FinanceManager.Api.Features.Dashboard.Extensions;
using FinanceManager.Api.Features.Transactions.Shared.Validation;
using FinanceManager.Api.Shared.Helpers;
using FinanceManager.Application.Alerts.Services;
using FinanceManager.Application.FinancialAccounts.Bond.Import;
using FinanceManager.Application.FinancialAccounts.Shared.Exports;
using FinanceManager.Application.Identity.Users;
using FinanceManager.Contracts.Dashboard;
using FinanceManager.Contracts.Transactions.Bonds;
using FinanceManager.Domain.Dashboard.Dtos;
using FinanceManager.Domain.FinancialAccounts.Bond.Dtos;
using FinanceManager.Domain.FinancialAccounts.Bond.Entities;
using FinanceManager.Domain.FinancialAccounts.Bond.Exports;
using FinanceManager.Domain.FinancialAccounts.Bond.Imports;
using FinanceManager.Domain.FinancialAccounts.Bond.Repositories;
using FinanceManager.Domain.FinancialAccounts.Shared.Entities;
using FinanceManager.Domain.Labels.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using System.Text;

namespace FinanceManager.Api.Features.Transactions.Bonds.Controllers;

/// <summary>
/// The caller's bond entries pooled per bond definition. An entry belongs to its owner (<c>UserId</c>) and needs no
/// account; an entry of someone else is reported as not found.
/// </summary>
[Authorize]
[ApiController]
[Route("api/transactions/bonds")]
[Tags("Owner Bond Transactions")]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
public class BondTransactionsController(
    IBondAccountEntryRepository<BondAccountEntry> entryRepository,
    IBondDetailsRepository bondDetailsRepository,
    IFinancialLabelsRepository labelsRepository,
    IUserPlanVerifier userPlanVerifier,
    IBondAccountImportService importService,
    IFinancialAlertService financialAlertService,
    IAccountCsvExportService<BondAccountExportDto> csvExportService) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType<BondTransactionLogItemDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create(SaveBondEntryRequest request, CancellationToken cancellationToken = default)
    {
        if (!ApiAuthenticationHelper.TryGetOwnerId(User, out var ownerId)) return Forbid();

        OwnerEntryRequestValidation.ValidateFields(ModelState, request.PostingDate, request.Description, descriptionRequired: false);
        if (!ModelState.IsValid)
            return ValidationProblem(statusCode: StatusCodes.Status400BadRequest, modelStateDictionary: ModelState);
        var bond = (await bondDetailsRepository.GetByIdsAsync([request.BondDetailsId], cancellationToken)).FirstOrDefault();
        if (bond is null) return BadRequest("Unknown bond definition.");
        if (await OwnerEntryRequestValidation.ResolveLabels(labelsRepository, request.LabelIds, cancellationToken) is not List<FinancialLabel> labels)
            return BadRequest("Unknown label ID.");
        if (!await userPlanVerifier.CanAddMoreEntries(ownerId))
            return BadRequest("Too many entries. In order to add this entry upgrade to higher tier or delete existing one.");

        var entry = new BondAccountEntry(null, 0, request.PostingDate, 0, request.ValueChange, bond.Id)
        {
            UserId = ownerId,
            Description = request.Description ?? string.Empty,
            Labels = labels,
        };
        if (!await entryRepository.Add(entry, recalculate: false, cancellationToken)) return BadRequest("Cannot add this entry.");

        await financialAlertService.EvaluateAlertsAsync(ownerId, cancellationToken);
        return await Persisted(ownerId, entry.EntryId, cancellationToken);
    }

    [HttpPut("{entryId:long}")]
    [ProducesResponseType<BondTransactionLogItemDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(long entryId, SaveBondEntryRequest request, CancellationToken cancellationToken = default)
    {
        if (!ApiAuthenticationHelper.TryGetOwnerId(User, out var ownerId)) return Forbid();

        OwnerEntryRequestValidation.ValidateFields(ModelState, request.PostingDate, request.Description, descriptionRequired: false);
        if (!ModelState.IsValid)
            return ValidationProblem(statusCode: StatusCodes.Status400BadRequest, modelStateDictionary: ModelState);

        var existing = await entryRepository.GetByOwner(ownerId, entryId, cancellationToken);
        if (existing is null) return NotFound();

        // An unchanged definition stays valid even when its catalog record has since gone.
        if (request.BondDetailsId != existing.BondDetailsId
            && (await bondDetailsRepository.GetByIdsAsync([request.BondDetailsId], cancellationToken)).Count == 0)
            return BadRequest("Unknown bond definition.");

        // Omitted label ids keep the labels; an explicit list, even an empty one, replaces them.
        var labels = request.LabelIds is null
            ? existing.Labels.ToList()
            : await OwnerEntryRequestValidation.ResolveLabels(labelsRepository, request.LabelIds, cancellationToken);
        if (labels is null) return BadRequest("Unknown label ID.");

        // The owner and account link never come from the request.
        var entry = new BondAccountEntry(existing.AccountId, entryId, request.PostingDate, existing.Value, request.ValueChange, request.BondDetailsId)
        {
            UserId = ownerId,
            Description = request.Description ?? string.Empty,
            Labels = labels,
        };
        if (!await entryRepository.UpdateByOwner(ownerId, entry, cancellationToken)) return NotFound();

        await financialAlertService.EvaluateAlertsAsync(ownerId, cancellationToken);
        return await Persisted(ownerId, entryId, cancellationToken);
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

    /// <summary>Imports rows for any of the caller's bond definitions. Rows that overlap existing entries are returned as conflicts.</summary>
    [HttpPost("import")]
    [RequestSizeLimit(RequestBodySizeLimits.ImportEndpointBytes)]
    [ProducesResponseType<BondImportResult>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Import([FromBody] IReadOnlyList<BondEntryImportRecordDto> entries, CancellationToken cancellationToken = default)
    {
        if (!ApiAuthenticationHelper.TryGetOwnerId(User, out var ownerId)) return Forbid();
        if (entries is null || entries.Count == 0) return BadRequest("No import data provided.");
        if (!await AllDefinitionsExist(entries, cancellationToken)) return BadRequest("Unknown bond definition.");

        try
        {
            var result = await importService.ImportOwnerEntries(ownerId, ToImports(entries), cancellationToken);
            await financialAlertService.EvaluateAlertsAsync(ownerId, cancellationToken);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            // The plan limit is checked for the whole input before anything is written.
            return BadRequest(ex.Message);
        }
    }

    /// <summary>
    /// Adds what is still missing for the incoming rows the caller accepted after reviewing import conflicts. Submit
    /// the original rows of each accepted key, including the occurrences that already exist; only the missing ones are added.
    /// </summary>
    [HttpPost("import/resolve")]
    [RequestSizeLimit(RequestBodySizeLimits.ImportEndpointBytes)]
    [ProducesResponseType<BondImportResult>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ResolveImport([FromBody] IReadOnlyList<BondEntryImportRecordDto> acceptedEntries, CancellationToken cancellationToken = default)
    {
        if (!ApiAuthenticationHelper.TryGetOwnerId(User, out var ownerId)) return Forbid();
        if (acceptedEntries is null || acceptedEntries.Count == 0) return BadRequest("No accepted rows provided.");
        if (!await AllDefinitionsExist(acceptedEntries, cancellationToken)) return BadRequest("Unknown bond definition.");

        try
        {
            var result = await importService.ApplyOwnerConflicts(ownerId, ToImports(acceptedEntries), cancellationToken);
            await financialAlertService.EvaluateAlertsAsync(ownerId, cancellationToken);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpGet("export")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Export(
        [FromQuery, BindRequired] int bondDetailsId,
        [FromQuery, BindRequired] DateTime startDate,
        [FromQuery, BindRequired] DateTime endDate,
        CancellationToken cancellationToken = default)
    {
        if (!ApiAuthenticationHelper.TryGetOwnerId(User, out var ownerId)) return Forbid();

        var start = TransactionLogQuery.AsUtc(startDate);
        var end = TransactionLogQuery.AsUtc(endDate);
        if (start > end) return BadRequest("Start must be on or before end.");
        if (await bondDetailsRepository.GetByIdAsync(bondDetailsId, cancellationToken) is null) return BadRequest("Unknown bond definition.");

        var csv = await csvExportService.GetOwnerExportResults(ownerId, bondDetailsId, start, end, cancellationToken);
        var fileName = $"bond-{bondDetailsId}-{start:yyyyMMdd}-{end:yyyyMMdd}.csv";
        return File(Encoding.UTF8.GetBytes(csv), "text/csv", fileName);
    }

    private static IEnumerable<BondEntryImport> ToImports(IEnumerable<BondEntryImportRecordDto> records) =>
        records.Select(e => new BondEntryImport(e.PostingDate, e.ValueChange, e.BondDetailsId, e.Description ?? string.Empty));

    private async Task<bool> AllDefinitionsExist(IEnumerable<BondEntryImportRecordDto> records, CancellationToken cancellationToken)
    {
        var ids = records.Select(e => e.BondDetailsId).Distinct().ToList();
        return (await bondDetailsRepository.GetNamesByIdsAsync(ids, cancellationToken)).Count == ids.Count;
    }

    // The exact row the write produced, read back by its generated id with its labels and catalog metadata.
    private async Task<IActionResult> Persisted(int ownerId, long entryId, CancellationToken cancellationToken)
    {
        var saved = await entryRepository.GetByOwner(ownerId, entryId, cancellationToken);
        if (saved is null) return NotFound();

        var bond = (await bondDetailsRepository.GetByIdsAsync([saved.BondDetailsId], cancellationToken)).FirstOrDefault();
        return Ok(saved.ToLogItemDto(bond?.Currency.ShortName, bond?.Name));
    }
}