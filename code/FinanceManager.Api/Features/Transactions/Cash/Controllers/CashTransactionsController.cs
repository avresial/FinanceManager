using FinanceManager.Api.Features.Dashboard.Extensions;
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

        if (!await entryRepository.Add(entry, recalculate: false, cancellationToken)) return BadRequest("Cannot add this entry.");

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
        var entry = new CurrencyAccountEntry(existing.AccountId, entryId, request.PostingDate, existing.Value, request.ValueChange)
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
        await jobChannel.QueueJob(new CurrencyImportJobRequest(jobId, ownerId, null, entries, guestSessionAccessor.GuestUserId, currencyId), cancellationToken);

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
        if (guestSessionAccessor.IsGuest || ownerId < 0 || entry.EntryId == 0 || entry.Labels.Count != 0) return;
        if (string.IsNullOrWhiteSpace(entry.Description) && string.IsNullOrWhiteSpace(entry.ContractorDetails)) return;

        await labelSetterChannel.QueueEntries(ownerId, [entry.EntryId], cancellationToken);
    }

    // The exact row the write produced, read back by its generated id with its labels.
    private async Task<IActionResult> Persisted(int ownerId, long entryId, string? currencyCode, CancellationToken cancellationToken)
    {
        var saved = await entryRepository.GetByOwner(ownerId, entryId, cancellationToken);
        if (saved is null) return NotFound();

        currencyCode ??= saved.CurrencyId is int currencyId ? (await currencyRepository.GetCurrency(currencyId, cancellationToken))?.ShortName : null;
        return Ok(saved.ToLogItemDto(currencyCode));
    }
}